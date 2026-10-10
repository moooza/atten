using Microsoft.Data.Sqlite;
using Xunit;

namespace Atten.Data.Tests;

public class BackupTests
{
    [Fact]
    public void CommittedWriteCreatesAStandaloneSnapshot()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        int before = DatabaseBackup.SnapshotPaths(db.DbFile).Count;
        InsertPerson(db.DbFile, "dev-1");

        IReadOnlyList<string> copies = DatabaseBackup.SnapshotPaths(db.DbFile);
        Assert.Equal(before + 1, copies.Count);
        string backup = copies[^1];
        Assert.False(File.Exists(backup + "-wal"));
        using (SqliteConnection connection = OpenRaw(backup))
        {
            Assert.Equal("dev-1", Scalar(connection, "SELECT remote_id FROM personnel"));
            Assert.Equal("ok", Scalar(connection, "PRAGMA quick_check"));
        }

        PersonIds(db.DbFile);
        Assert.Equal(before + 1, DatabaseBackup.SnapshotPaths(db.DbFile).Count);
    }

    [Fact]
    public void DamagedDatabaseIsRestoredFromTheNewestHealthySnapshot()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        InsertPerson(db.DbFile, "kept");
        string junk = Path.Combine(db.Folder, "backups", "atten-2999-01-01T000000000000.db");
        File.WriteAllBytes(junk, "this is not a database"u8.ToArray().Concat(new byte[128]).ToArray());
        File.WriteAllBytes(db.DbFile, "torn write"u8.ToArray());
        File.WriteAllBytes(db.DbFile + "-wal", "stale wal"u8.ToArray());

        string? notice = DatabaseBackup.Prepare(db.DbFile);

        Assert.NotNull(notice);
        Assert.True(DatabaseBackup.IsHealthy(db.DbFile));
        Assert.Equal(["kept"], PersonIds(db.DbFile));
        string corrupt = Path.Combine(db.Folder, "corrupt");
        IReadOnlyList<string> quarantined = Directory.GetFiles(corrupt);
        Assert.Contains(quarantined, path => Path.GetFileName(path).StartsWith("atten.db.", StringComparison.Ordinal));
        IReadOnlyList<string> walCopies = quarantined
            .Where(path => Path.GetFileName(path).StartsWith("atten.db-wal.", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(["stale wal"u8.ToArray()], walCopies.Select(File.ReadAllBytes).ToList());
    }

    [Fact]
    public void DamagedDatabaseWithoutASnapshotIsLeftInPlace()
    {
        using var db = new TempDatabase();
        byte[] original = "not a database"u8.ToArray();
        File.WriteAllBytes(db.DbFile, original);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => DatabaseBackup.Prepare(db.DbFile));
        Assert.Equal(DatabaseBackup.DamagedWithoutBackup, error.Message);

        Assert.Equal(original, File.ReadAllBytes(db.DbFile));
        Assert.False(Directory.Exists(Path.Combine(db.Folder, "corrupt")));
    }

    [Fact]
    public void SnapshotsArePrunedToTheRequestedCount()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        for (int index = 0; index < 4; index++)
        {
            Assert.NotNull(DatabaseBackup.Snapshot(db.DbFile, keep: 2));
        }

        Assert.Equal(2, DatabaseBackup.SnapshotPaths(db.DbFile).Count);
    }

    [Fact]
    public void ExportNamesTheFileWithTheCreationTimeAndKeepsEveryRow()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        InsertPerson(db.DbFile, "dev-1");
        string folder = Path.Combine(db.Folder, "chosen");
        DateTime moment = new(2026, 10, 8, 21, 28, 5);

        string exported = DatabaseBackup.Export(db.DbFile, folder, moment);
        string again = DatabaseBackup.Export(db.DbFile, folder, moment);

        Assert.Equal("atten-backup-2026-10-08_21-28-05.db", Path.GetFileName(exported));
        Assert.Equal("atten-backup-2026-10-08_21-28-05-2.db", Path.GetFileName(again));
        Assert.Equal(folder, Path.GetDirectoryName(exported));
        using SqliteConnection copy = OpenRaw(exported);
        Assert.Equal("dev-1", Scalar(copy, "SELECT remote_id FROM personnel"));
        Assert.NotNull(Scalar(copy, "SELECT 1 FROM schema_migrations"));
        Assert.Equal("ok", Scalar(copy, "PRAGMA quick_check"));
    }

    [Fact]
    public void RestoreReplacesTheLiveDatabaseAndDropsTheWal()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        InsertPerson(db.DbFile, "kept");
        string exported = DatabaseBackup.Export(
            db.DbFile,
            Path.Combine(db.Folder, "out"),
            new DateTime(2026, 10, 8, 21, 28, 5));
        InsertPerson(db.DbFile, "gone");
        File.WriteAllBytes(db.DbFile + "-wal", "stale wal"u8.ToArray());

        DatabaseBackup.Restore(db.DbFile, exported);

        Assert.Equal(["kept"], PersonIds(db.DbFile));
        Assert.True(DatabaseBackup.IsHealthy(db.DbFile));
        Assert.False(File.Exists(db.DbFile + "-wal"));
        Assert.False(File.Exists(db.DbFile + ".restore"));
    }

    [Fact]
    public void RestoreRejectsAFileThatIsNotABackup()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        InsertPerson(db.DbFile, "kept");
        string junk = Path.Combine(db.Folder, "notes.db");
        File.WriteAllBytes(junk, "this is not a database"u8.ToArray().Concat(new byte[128]).ToArray());

        ArgumentException error = Assert.Throws<ArgumentException>(() => DatabaseBackup.Restore(db.DbFile, junk));
        Assert.Contains("نسخهٔ پشتیبان سالم", error.Message, StringComparison.Ordinal);

        Assert.Equal(["kept"], PersonIds(db.DbFile));
    }

    [Fact]
    public void UserExportsAreNotPrunedWithAutomaticSnapshots()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        string exported = DatabaseBackup.Export(
            db.DbFile,
            Path.Combine(db.Folder, "backups"),
            new DateTime(2026, 10, 8, 21, 28, 5));
        for (int index = 0; index < 3; index++)
        {
            Assert.NotNull(DatabaseBackup.Snapshot(db.DbFile, keep: 1));
        }

        Assert.True(File.Exists(exported));
        Assert.Single(DatabaseBackup.SnapshotPaths(db.DbFile));
        Assert.DoesNotContain(exported, DatabaseBackup.SnapshotPaths(db.DbFile));
    }

    [Fact]
    public void CheckBackupReadsEveryTableAndReportsABrokenLink()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        InsertPerson(db.DbFile, "dev-1");
        string exported = DatabaseBackup.Export(
            db.DbFile,
            Path.Combine(db.Folder, "out"),
            new DateTime(2026, 10, 8, 21, 28, 5));
        byte[] original = File.ReadAllBytes(exported);
        var steps = new List<(int Done, int Total, string Message)>();

        BackupHealth result = DatabaseBackup.Check(
            exported,
            (done, total, message) => steps.Add((done, total, message)));

        Assert.True(result.Ok);
        Dictionary<string, int> counts = result.Tables.ToDictionary(table => table.Name, table => table.Rows);
        Assert.Equal(1, counts["personnel"]);
        Assert.Equal(0, counts["clock_events"]);
        Assert.Equal(0, counts["leaves"]);
        Assert.Contains("schema_migrations", counts.Keys);
        Assert.Equal((steps[^1].Total, steps[^1].Total, "بررسی تمام شد."), (steps[^1].Done, steps[^1].Total, steps[^1].Message));
        Assert.Contains(steps, step => step.Message.Contains("پرسنل", StringComparison.Ordinal));
        string description = DatabaseBackup.Describe(result);
        Assert.Contains("فایل سالم است", description, StringComparison.Ordinal);
        Assert.Contains("پرسنل: 1 رکورد", description, StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(exported));
        Assert.False(File.Exists(exported + "-wal"));

        using (SqliteConnection connection = OpenRaw(exported, SqliteOpenMode.ReadWrite))
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO leaves (personnel_id, start_date, end_date, minutes, created_at, updated_at)
                VALUES (999, '2026-10-08', '2026-10-08', 60, '2026-10-08T10:00:00', '2026-10-08T10:00:00')
                """;
            command.ExecuteNonQuery();
        }

        BackupHealth broken = DatabaseBackup.Check(exported);
        Assert.False(broken.Ok);
        string text = DatabaseBackup.Describe(broken);
        Assert.Contains("فایل سالم نیست.", text, StringComparison.Ordinal);
        Assert.Contains("مرخصی‌ها", text, StringComparison.Ordinal);
        Assert.Contains("پرسنل", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckBackupRejectsAFileThatIsNotADatabase()
    {
        using var db = new TempDatabase();
        string junk = Path.Combine(db.Folder, "notes.db");
        File.WriteAllBytes(junk, "this is not a database"u8.ToArray().Concat(new byte[128]).ToArray());
        string missing = Path.Combine(db.Folder, "missing.db");

        BackupHealth junkResult = DatabaseBackup.Check(junk);
        BackupHealth missingResult = DatabaseBackup.Check(missing);

        Assert.False(junkResult.Ok);
        Assert.False(junkResult.Readable);
        Assert.Equal("این فایل پایگاه دادهٔ سالمی نیست.", junkResult.FileError);
        Assert.False(missingResult.Readable);
        Assert.Equal("فایل پیدا نشد.", missingResult.FileError);

        Migrator.Apply(db.DbFile);
        string exported = DatabaseBackup.Export(
            db.DbFile,
            Path.Combine(db.Folder, "out"),
            new DateTime(2026, 10, 8, 21, 28, 5));
        string truncated = Path.Combine(db.Folder, "truncated.db");
        File.WriteAllBytes(truncated, File.ReadAllBytes(exported).Take(120).ToArray());
        BackupHealth damaged = DatabaseBackup.Check(truncated);
        Assert.False(damaged.Ok);
        Assert.DoesNotContain("فایل سالم است", DatabaseBackup.Describe(damaged), StringComparison.Ordinal);
    }

    [Fact]
    public void StoreBackupServiceExportsAndDescribesThroughTheInterface()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        store.Personnel.Add("علی", "رضایی", 8, remoteId: "dev-1");
        string folder = Path.Combine(db.Folder, "chosen");
        DateTime moment = new(2026, 10, 8, 21, 28, 5);

        string exported = store.Backup.Export(folder, moment);
        BackupHealth health = store.Backup.Check(exported);

        Assert.Equal("atten-backup-2026-10-08_21-28-05.db", Path.GetFileName(exported));
        Assert.True(health.Ok);
        Assert.Contains("فایل سالم است", store.Backup.Describe(health), StringComparison.Ordinal);
        Assert.Equal(db.Folder, store.Backup.SuggestedFolder);
    }

    [Fact]
    public void StartReturnsRestoredNoticeAfterRepairingADamagedFile()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        InsertPerson(db.DbFile, "kept");
        File.WriteAllBytes(db.DbFile, "torn write"u8.ToArray());

        IAttenRepository store = AttenStore.Start(db.DbFile, DefaultUser.Id, out string? notice);

        Assert.Equal(DatabaseBackup.RestoredNotice, notice);
        Assert.Equal("kept", Assert.Single(store.Personnel.List()).RemoteId);
    }

    private static void InsertPerson(string dbFile, string remoteId)
    {
        AttenStore.Open(dbFile).Personnel.Add("علی", "رضایی", 8, remoteId: remoteId);
    }

    private static List<string> PersonIds(string dbFile)
    {
        return AttenStore.Open(dbFile).Personnel.List()
            .Select(person => person.RemoteId ?? "")
            .ToList();
    }

    private static SqliteConnection OpenRaw(string path, SqliteOpenMode mode = SqliteOpenMode.ReadOnly)
    {
        return SqliteDatabase.OpenRaw(path, mode);
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
