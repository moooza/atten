using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Atten.Data;

/// <summary>
/// Crash-safe copies of the database, and restore when the live file is damaged.
/// File snapshots stay in this layer; App never sees WAL or atten.db paths.
/// </summary>
public static class DatabaseBackup
{
    public const int KeepSnapshots = 60;

    public const string RestoredNotice =
        "پایگاه داده آسیب دیده بود و از آخرین نسخهٔ پشتیبان سالم برگردانده شد. " +
        "اگر درست قبل از قطع برق چیزی ذخیره شده باشد، ممکن است همان ثبت آخر برنگشته باشد.";

    public const string DamagedWithoutBackup =
        "database is damaged and no backup is available";

    public const string ExportFailed = "ساختن نسخهٔ پشتیبان ممکن نشد.";

    public const string RestoreFailed = "بازگردانی نسخهٔ پشتیبان ممکن نشد.";

    public const string SameFile = "این فایل همان پایگاه دادهٔ جاری است.";

    public const string NotABackup = "فایل انتخاب‌شده یک نسخهٔ پشتیبان سالم نیست.";

    private const string SnapshotPrefix = "atten-";
    private const string SnapshotSuffix = ".db";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly IReadOnlyDictionary<string, string> TableLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schema_migrations"] = "نسخه‌های پایگاه",
            ["users"] = "کاربران",
            ["personnel"] = "پرسنل",
            ["clock_events"] = "ورود و خروج",
            ["leaves"] = "مرخصی‌ها",
            ["calculation_days"] = "روزهای محاسبه",
            ["calculation_punches"] = "ساعت‌های محاسبه",
            ["payroll_runs"] = "حقوق",
        };

    private static readonly string[] TableOrder = [.. TableLabels.Keys];

    /// <summary>Migrate a healthy file, or replace a damaged one with the newest good snapshot.</summary>
    public static string? Prepare(string dbFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);

        string? notice = null;
        if (File.Exists(dbFile) && !IsHealthy(dbFile))
        {
            string? restored = RestoreNewest(dbFile);
            if (restored is null)
            {
                throw new InvalidOperationException(DamagedWithoutBackup);
            }

            notice = RestoredNotice;
        }

        Migrator.Apply(dbFile);
        if (SnapshotPaths(dbFile).Count == 0)
        {
            Snapshot(dbFile);
        }

        return notice;
    }

    /// <summary>Write a consistent single-file copy beside the database.</summary>
    public static string? Snapshot(string dbFile, int keep = KeepSnapshots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        if (!File.Exists(dbFile))
        {
            return null;
        }

        string folder = Path.Combine(ParentDirectory(dbFile), "backups");
        Directory.CreateDirectory(folder);
        string target = NextSnapshotPath(folder);
        if (!ConsistentCopy(dbFile, target, checkpointSource: true))
        {
            return null;
        }

        Prune(folder, keep);
        return target;
    }

    internal static void TrySnapshot(string dbFile)
    {
        try
        {
            Snapshot(dbFile);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Replace a damaged database with the newest snapshot that still opens.</summary>
    public static string? RestoreNewest(string dbFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        IReadOnlyList<string> candidates = SnapshotPaths(dbFile)
            .OrderByDescending(path => new FileInfo(path).LastWriteTimeUtc)
            .ToList();
        foreach (string backup in candidates)
        {
            if (!IsHealthy(backup))
            {
                continue;
            }

            Quarantine(dbFile);
            File.Copy(backup, dbFile, overwrite: true);
            Fsync(dbFile);
            if (IsHealthy(dbFile))
            {
                return backup;
            }
        }

        return null;
    }

    public static bool IsHealthy(string dbFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        if (!File.Exists(dbFile))
        {
            return false;
        }

        try
        {
            if (new FileInfo(dbFile).Length < 100)
            {
                return false;
            }
        }
        catch (IOException)
        {
            return false;
        }

        try
        {
            using SqliteConnection connection = SqliteDatabase.OpenRaw(dbFile, SqliteOpenMode.ReadWrite);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check";
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return false;
            }

            string value = reader.GetString(0);
            return !reader.Read() && string.Equals(value, "ok", StringComparison.Ordinal);
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    /// <summary>Write a consistent copy of the whole database into the chosen folder.</summary>
    public static string Export(string dbFile, string folder, DateTime? moment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!File.Exists(dbFile))
        {
            throw new FileNotFoundException(dbFile, dbFile);
        }

        Directory.CreateDirectory(folder);
        string target = NextExportPath(folder, moment ?? DateTime.Now);
        if (!ConsistentCopy(dbFile, target, checkpointSource: true))
        {
            throw new InvalidOperationException(ExportFailed);
        }

        return target;
    }

    /// <summary>Reject a file that cannot replace the live database.</summary>
    public static void Validate(string dbFile, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        string resolvedSource = Path.GetFullPath(source);
        string resolvedLive = Path.GetFullPath(dbFile);
        if (string.Equals(resolvedSource, resolvedLive, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(SameFile);
        }

        if (!IsAppDatabase(resolvedSource))
        {
            throw new ArgumentException(NotABackup);
        }
    }

    /// <summary>Replace the live database with a consistent copy of a backup file.</summary>
    public static void Restore(string dbFile, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        string resolvedSource = Path.GetFullPath(source);
        string resolvedLive = Path.GetFullPath(dbFile);
        Validate(resolvedLive, resolvedSource);
        string? parent = Path.GetDirectoryName(resolvedLive);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        string temporary = resolvedLive + ".restore";
        try
        {
            if (!ConsistentCopy(resolvedSource, temporary, checkpointSource: false))
            {
                throw new InvalidOperationException(RestoreFailed);
            }

            DiscardSidecars(resolvedLive);
            File.Move(temporary, resolvedLive, overwrite: true);
            RemoveSidecars(resolvedLive);
            Fsync(resolvedLive);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            RemoveSidecars(temporary);
        }

        Migrator.Apply(resolvedLive);
    }

    public static string TableLabel(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return TableLabels.TryGetValue(name, out string? label) ? label : name;
    }

    /// <summary>Read every table and record, then check pages, indexes, and foreign keys.</summary>
    public static BackupHealth Check(string path, Action<int, int, string>? onProgress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        void Report(int done, int total, string message)
        {
            onProgress?.Invoke(done, total, message);
        }

        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.Directory) != 0)
        {
            Report(1, 1, "فایل پیدا نشد.");
            return BackupHealth.Unreadable("فایل پیدا نشد.");
        }

        SqliteConnection connection;
        try
        {
            connection = SqliteDatabase.OpenRaw(path, SqliteOpenMode.ReadOnly);
        }
        catch (SqliteException ex)
        {
            Report(1, 1, "فایل باز نشد.");
            return BackupHealth.Unreadable(FileError(ex));
        }

        try
        {
            List<string> names;
            try
            {
                names = TableNames(connection);
            }
            catch (SqliteException ex)
            {
                Report(1, 1, "فایل پایگاه داده نیست.");
                return BackupHealth.Unreadable(FileError(ex));
            }

            IReadOnlyList<string> ordered = OrderedTables(names);
            int total = ordered.Count + 2;
            var tables = new List<CheckedTable>();
            for (int index = 0; index < ordered.Count; index++)
            {
                string name = ordered[index];
                string label = TableLabel(name);
                int step = index + 1;
                Report(index, total, $"جدول {label} بررسی می‌شود...");
                (int rows, string? error) = ScanTable(
                    connection,
                    name,
                    count => Report(index, total, $"جدول {label}: {count} رکورد خوانده شد..."));
                tables.Add(new CheckedTable(name, rows, error));
                Report(
                    step,
                    total,
                    error is null ? $"جدول {label}: {rows} رکورد" : $"جدول {label} خراب است.");
            }

            Report(ordered.Count, total, "ساختار فایل و فهرست‌ها بررسی می‌شود...");
            IReadOnlyList<string> integrityErrors = IntegrityErrors(connection);
            Report(ordered.Count + 1, total, "ارتباط جدول‌ها بررسی می‌شود...");
            IReadOnlyList<string> foreignKeyErrors = ForeignKeyErrors(connection);
            Report(total, total, "بررسی تمام شد.");
            return new BackupHealth(true, tables, integrityErrors, foreignKeyErrors, null);
        }
        finally
        {
            connection.Dispose();
        }
    }

    public static string Describe(BackupHealth result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Readable)
        {
            return result.FileError ?? "فایل قابل بررسی نیست.";
        }

        var lines = new List<string>();
        int totalRows = 0;
        foreach (CheckedTable table in result.Tables)
        {
            string label = TableLabel(table.Name);
            if (table.Error is not null)
            {
                lines.Add($"{label}: {table.Error}");
            }
            else
            {
                lines.Add($"{label}: {table.Rows} رکورد");
                totalRows += table.Rows;
            }
        }

        if (!result.Tables.Any(table => table.Name == "schema_migrations"))
        {
            lines.Add("این فایل نسخهٔ پشتیبان این برنامه نیست.");
        }

        lines.Add(
            result.IntegrityErrors.Count > 0
                ? "ساختار فایل: " + JoinFindings(result.IntegrityErrors)
                : "ساختار فایل و فهرست‌ها: سالم");
        lines.Add(
            result.ForeignKeyErrors.Count > 0
                ? "ارتباط جدول‌ها: " + JoinFindings(result.ForeignKeyErrors)
                : "ارتباط جدول‌ها: سالم");
        if (result.Ok)
        {
            lines.Insert(0, $"فایل سالم است. {result.Tables.Count} جدول و {totalRows} رکورد بررسی شد.");
        }
        else
        {
            lines.Insert(0, "فایل سالم نیست.");
        }

        return string.Join("\n", lines);
    }

    public static IReadOnlyList<string> SnapshotPaths(string dbFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        string folder = Path.Combine(ParentDirectory(dbFile), "backups");
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.GetFiles(folder)
            .Where(path => IsSnapshotName(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool ConsistentCopy(string sourceFile, string target, bool checkpointSource)
    {
        string temporary = target + ".tmp";
        bool failed = false;
        SqliteConnection? source = null;
        SqliteConnection? destination = null;
        try
        {
            source = SqliteDatabase.OpenRaw(sourceFile, SqliteOpenMode.ReadWrite);
            destination = SqliteDatabase.OpenRaw(temporary, SqliteOpenMode.ReadWriteCreate);
            Execute(destination, "PRAGMA synchronous = FULL");
            source.BackupDatabase(destination);
            ExecuteScalar(destination, "PRAGMA journal_mode = DELETE");
        }
        catch (SqliteException)
        {
            failed = true;
        }
        finally
        {
            destination?.Dispose();
            RemoveSidecars(temporary);
            if (checkpointSource && source is not null)
            {
                try
                {
                    ExecuteReader(source, "PRAGMA wal_checkpoint(TRUNCATE)");
                }
                catch (SqliteException)
                {
                }
            }

            source?.Dispose();
        }

        if (failed || !File.Exists(temporary) || !IsHealthy(temporary))
        {
            TryDelete(temporary);
            RemoveSidecars(temporary);
            return false;
        }

        RemoveSidecars(temporary);
        Fsync(temporary);
        File.Move(temporary, target, overwrite: true);
        return true;
    }

    private static bool IsAppDatabase(string dbFile)
    {
        if (!IsHealthy(dbFile))
        {
            return false;
        }

        try
        {
            using SqliteConnection connection = SqliteDatabase.OpenRaw(dbFile, SqliteOpenMode.ReadWrite);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations'";
            return command.ExecuteScalar() is not null;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static List<string> TableNames(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static IReadOnlyList<string> OrderedTables(IReadOnlyList<string> names)
    {
        var ranked = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < TableOrder.Length; index++)
        {
            ranked[TableOrder[index]] = index;
        }

        return names
            .OrderBy(name => ranked.TryGetValue(name, out int rank) ? rank : ranked.Count)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static (int Rows, string? Error) ScanTable(
        SqliteConnection connection,
        string name,
        Action<int> onBatch)
    {
        string quoted = "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        try
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {quoted}";
            using SqliteDataReader reader = command.ExecuteReader();
            int count = 0;
            int announced = 0;
            while (reader.Read())
            {
                count++;
                if (count - announced >= 1000)
                {
                    announced = count;
                    onBatch(count);
                }
            }

            return (count, null);
        }
        catch (SqliteException)
        {
            return (0, "خواندن رکوردها ممکن نشد.");
        }
    }

    private static IReadOnlyList<string> IntegrityErrors(SqliteConnection connection)
    {
        try
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";
            using SqliteDataReader reader = command.ExecuteReader();
            var messages = new List<string>();
            while (reader.Read())
            {
                string value = reader.GetString(0);
                if (!string.Equals(value, "ok", StringComparison.Ordinal))
                {
                    messages.Add(value);
                }
            }

            return messages;
        }
        catch (SqliteException)
        {
            return ["ساختار فایل خراب است."];
        }
    }

    private static IReadOnlyList<string> ForeignKeyErrors(SqliteConnection connection)
    {
        try
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_key_check";
            using SqliteDataReader reader = command.ExecuteReader();
            var findings = new List<string>();
            while (reader.Read())
            {
                string table = TableLabel(reader.GetString(0));
                string parent = TableLabel(reader.GetString(2));
                findings.Add($"جدول {table} به {parent} وصل نیست (ردیف {reader.GetValue(1)}).");
            }

            return findings;
        }
        catch (SqliteException)
        {
            return ["ارتباط جدول‌ها بررسی نشد."];
        }
    }

    private static string FileError(SqliteException exception)
    {
        string text = exception.Message.ToLowerInvariant();
        if (text.Contains("not a database", StringComparison.Ordinal)
            || text.Contains("malformed", StringComparison.Ordinal)
            || text.Contains("disk image", StringComparison.Ordinal))
        {
            return "این فایل پایگاه دادهٔ سالمی نیست.";
        }

        if (text.Contains("unable to open", StringComparison.Ordinal))
        {
            return "فایل باز نشد.";
        }

        return "این فایل پایگاه دادهٔ سالمی نیست.";
    }

    private static string JoinFindings(IReadOnlyList<string> findings)
    {
        IEnumerable<string> shown = findings.Take(8);
        string text = string.Join("؛ ", shown);
        int extra = findings.Count - Math.Min(findings.Count, 8);
        if (extra > 0)
        {
            text += $"؛ و {extra} مورد دیگر";
        }

        return text;
    }

    private static string NextExportPath(string folder, DateTime moment)
    {
        string stamp = moment.ToString("yyyy-MM-dd_HH-mm-ss", Invariant);
        string path = Path.Combine(folder, $"atten-backup-{stamp}.db");
        int counter = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(folder, $"atten-backup-{stamp}-{counter}.db");
            counter++;
        }

        return path;
    }

    private static string NextSnapshotPath(string folder)
    {
        string stamp = DateTime.Now.ToString("yyyy-MM-ddTHHmmssffffff", Invariant);
        string path = Path.Combine(folder, $"{SnapshotPrefix}{stamp}{SnapshotSuffix}");
        int counter = 2;
        while (File.Exists(path))
        {
            path = Path.Combine(folder, $"{SnapshotPrefix}{stamp}-{counter}{SnapshotSuffix}");
            counter++;
        }

        return path;
    }

    private static void DiscardSidecars(string dbFile)
    {
        if (File.Exists(dbFile))
        {
            try
            {
                using SqliteConnection connection = SqliteDatabase.OpenRaw(dbFile, SqliteOpenMode.ReadWrite);
                ExecuteReader(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
            }
            catch (SqliteException)
            {
            }
        }

        RemoveSidecars(dbFile);
    }

    private static void Prune(string folder, int keep)
    {
        List<string> files = Directory.GetFiles(folder)
            .Where(path => IsSnapshotName(Path.GetFileName(path)))
            .OrderBy(path => new FileInfo(path).LastWriteTimeUtc)
            .ToList();
        IEnumerable<string> extras = keep > 0 ? files.Take(Math.Max(0, files.Count - keep)) : files;
        foreach (string old in extras)
        {
            TryDelete(old);
        }
    }

    private static void Quarantine(string dbFile)
    {
        string folder = Path.Combine(ParentDirectory(dbFile), "corrupt");
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("yyyy-MM-ddTHHmmssffffff", Invariant);
        foreach (string path in Sidecars(dbFile).Prepend(dbFile))
        {
            if (File.Exists(path))
            {
                File.Move(path, Path.Combine(folder, $"{Path.GetFileName(path)}.{stamp}"), overwrite: true);
            }
        }
    }

    private static bool IsSnapshotName(string name)
    {
        return name.StartsWith(SnapshotPrefix, StringComparison.Ordinal)
            && name.EndsWith(SnapshotSuffix, StringComparison.Ordinal)
            && name.Length > SnapshotPrefix.Length
            && char.IsDigit(name[SnapshotPrefix.Length]);
    }

    private static IEnumerable<string> Sidecars(string dbFile)
    {
        yield return dbFile + "-wal";
        yield return dbFile + "-shm";
    }

    private static void RemoveSidecars(string dbFile)
    {
        foreach (string path in Sidecars(dbFile))
        {
            TryDelete(path);
        }
    }

    private static void Fsync(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        stream.Flush(true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ParentDirectory(string dbFile)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(dbFile));
        return string.IsNullOrEmpty(parent)
            ? throw new InvalidOperationException(dbFile)
            : parent;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void ExecuteScalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteScalar();
    }

    private static void ExecuteReader(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
        }
    }
}

public sealed record CheckedTable(string Name, int Rows, string? Error = null);

public sealed record BackupHealth(
    bool Readable,
    IReadOnlyList<CheckedTable> Tables,
    IReadOnlyList<string> IntegrityErrors,
    IReadOnlyList<string> ForeignKeyErrors,
    string? FileError)
{
    public static BackupHealth Unreadable(string fileError)
    {
        return new BackupHealth(false, [], [], [], fileError);
    }

    public bool Ok =>
        Readable
        && FileError is null
        && Tables.Any(table => table.Name == "schema_migrations")
        && Tables.All(table => table.Error is null)
        && IntegrityErrors.Count == 0
        && ForeignKeyErrors.Count == 0;
}

internal sealed class SqliteBackupService : IBackupService
{
    private readonly string _dbFile;

    public SqliteBackupService(string dbFile)
    {
        _dbFile = dbFile;
    }

    public string SuggestedFolder => ParentOf(_dbFile);

    public string Export(string folder, DateTime? moment = null)
    {
        return DatabaseBackup.Export(_dbFile, folder, moment);
    }

    public void Validate(string source)
    {
        DatabaseBackup.Validate(_dbFile, source);
    }

    public void Restore(string source)
    {
        DatabaseBackup.Restore(_dbFile, source);
    }

    public BackupHealth Check(string path, Action<int, int, string>? onProgress = null)
    {
        return DatabaseBackup.Check(path, onProgress);
    }

    public string Describe(BackupHealth result)
    {
        return DatabaseBackup.Describe(result);
    }

    private static string ParentOf(string dbFile)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(dbFile));
        return string.IsNullOrEmpty(parent) ? "" : parent;
    }
}
