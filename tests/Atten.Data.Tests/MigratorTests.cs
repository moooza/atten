using Microsoft.Data.Sqlite;
using Xunit;

namespace Atten.Data.Tests;

public class MigratorTests
{
    private static readonly string[] AllVersions =
    [
        "001", "002", "003", "004", "005", "006", "007", "008", "009", "010", "011", "012", "013",
    ];

    [Fact]
    public void MigrateCreatesSchemaAndIsIdempotent()
    {
        using var db = new TempDatabase();
        IReadOnlyList<string> first = Migrator.Apply(db.DbFile);
        IReadOnlyList<string> second = Migrator.Apply(db.DbFile);

        Assert.Equal(AllVersions, first);
        Assert.Empty(second);

        SqliteDatabase.Use(db.DbFile, connection =>
        {
            Assert.Equal(AllVersions, Versions(connection));
            Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode")?.ToString()?.ToLowerInvariant());
            Assert.Equal(2L, Number(connection, "PRAGMA synchronous"));
            Assert.Equal(1L, Number(connection, "PRAGMA foreign_keys"));
            Assert.Equal(5000L, Number(connection, "PRAGMA busy_timeout"));
            IReadOnlyList<object[]> clockKeys = Rows(connection, "PRAGMA foreign_key_list(clock_events)");
            Assert.DoesNotContain(clockKeys, row => Convert.ToString(row[2], System.Globalization.CultureInfo.InvariantCulture) == "personnel");
            Assert.All(clockKeys, row => Assert.Equal("users", Convert.ToString(row[2], System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal(
                [
                    "id",
                    "remote_id",
                    "first_name",
                    "last_name",
                    "daily_hours",
                    "mobile",
                    "created_at",
                    "updated_at",
                    "cooperation_start",
                    "cooperation_end",
                    "created_by",
                    "updated_by",
                ],
                Columns(connection, "personnel"));
            Assert.Equal(
                [
                    "id",
                    "remote_id",
                    "name",
                    "date",
                    "time",
                    "created_at",
                    "updated_at",
                    "created_by",
                    "updated_by",
                ],
                Columns(connection, "clock_events"));
            Assert.Equal(
                [
                    "id",
                    "remote_id",
                    "date",
                    "balance_minutes",
                    "created_at",
                    "updated_at",
                    "holiday",
                    "created_by",
                    "updated_by",
                ],
                Columns(connection, "calculation_days"));
            Assert.Equal(
                [
                    "id",
                    "remote_id",
                    "date",
                    "slot",
                    "time",
                    "created_at",
                    "updated_at",
                    "created_by",
                    "updated_by",
                ],
                Columns(connection, "calculation_punches"));
            Assert.Equal(
                [
                    "id",
                    "personnel_id",
                    "start_date",
                    "end_date",
                    "minutes",
                    "created_at",
                    "updated_at",
                    "created_by",
                    "updated_by",
                ],
                Columns(connection, "leaves"));
            Assert.Equal(["id", "display_name", "created_at"], Columns(connection, "users"));
            Assert.Equal(
                [
                    "id",
                    "personnel_id",
                    "start_date",
                    "end_date",
                    "overtime_minutes",
                    "deficit_minutes",
                    "leave_minutes",
                    "remaining_leave_minutes",
                    "status",
                    "created_at",
                    "updated_at",
                    "created_by",
                    "updated_by",
                ],
                Columns(connection, "payroll_runs"));
        });
    }

    [Fact]
    public void Migration012AddsDefaultUserAndBackfillsActorColumns()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);

        SqliteDatabase.Use(db.DbFile, connection =>
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO personnel (remote_id, first_name, last_name, daily_hours, created_at, updated_at)
                VALUES ('dev-1', 'علی', 'رضایی', 8, '2026-10-07T16:30:00', '2026-10-07T16:30:00')
                """;
            insert.ExecuteNonQuery();
            Revert013(connection);
            Revert012(connection);
        });

        Assert.Equal(["012", "013"], Migrator.Apply(db.DbFile));

        SqliteDatabase.Use(db.DbFile, connection =>
        {
            using SqliteCommand user = connection.CreateCommand();
            user.CommandText = "SELECT id, display_name FROM users";
            using SqliteDataReader reader = user.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(DefaultUser.Id, reader.GetInt64(0));
            Assert.Equal(DefaultUser.DisplayName, reader.GetString(1));
            Assert.False(reader.Read());

            using SqliteCommand person = connection.CreateCommand();
            person.CommandText = "SELECT created_by, updated_by FROM personnel WHERE remote_id = 'dev-1'";
            using SqliteDataReader row = person.ExecuteReader();
            Assert.True(row.Read());
            Assert.Equal(DefaultUser.Id, row.GetInt64(0));
            Assert.Equal(DefaultUser.Id, row.GetInt64(1));
        });
    }

    [Fact]
    public void ExistingDatabaseAt011ReceivesActorAndPayrollMigrations()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);
        SqliteDatabase.Use(db.DbFile, connection =>
        {
            Revert013(connection);
            Revert012(connection);
        });

        Assert.Equal(["012", "013"], Migrator.Apply(db.DbFile));
        Assert.Empty(Migrator.Apply(db.DbFile));
    }

    [Fact]
    public void EmbeddedMigrationsCoverOriginalSchemaAndNewVersions()
    {
        IReadOnlyList<(string Version, string FileName, string Sql)> embedded = Migrator.EmbeddedMigrations();

        Assert.Equal(AllVersions, embedded.Select(item => item.Version).ToArray());
        Assert.All(
            embedded,
            item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Sql));
                Assert.StartsWith(item.Version + "_", item.FileName, StringComparison.Ordinal);
                Assert.EndsWith(".sql", item.FileName, StringComparison.Ordinal);
            });
        Assert.Contains(embedded, item => item.Version == "012" && item.FileName == "012_users.sql");
        Assert.Contains(embedded, item => item.Version == "013" && item.FileName == "013_payroll.sql");
    }

    [Fact]
    public void AppliedAtIsRecordedAsUtc()
    {
        using var db = new TempDatabase();
        Migrator.Apply(db.DbFile);

        SqliteDatabase.Use(db.DbFile, connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT applied_at FROM schema_migrations WHERE version = '012'";
            string appliedAt = Assert.IsType<string>(command.ExecuteScalar());
            DateTimeOffset parsed = DateTimeOffset.Parse(appliedAt, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(TimeSpan.Zero, parsed.Offset);
            Assert.True((DateTimeOffset.UtcNow - parsed).Duration() < TimeSpan.FromMinutes(1));
        });
    }

    private static void Revert013(SqliteConnection connection)
    {
        connection.Execute("DELETE FROM schema_migrations WHERE version = '013'");
        connection.Execute("DROP TABLE IF EXISTS payroll_runs");
    }

    private static void Revert012(SqliteConnection connection)
    {
        connection.Execute("DELETE FROM schema_migrations WHERE version = '012'");
        foreach (string column in new[] { "created_by", "updated_by" })
        {
            connection.Execute($"ALTER TABLE personnel DROP COLUMN {column}");
            connection.Execute($"ALTER TABLE clock_events DROP COLUMN {column}");
            connection.Execute($"ALTER TABLE leaves DROP COLUMN {column}");
            connection.Execute($"ALTER TABLE calculation_days DROP COLUMN {column}");
            connection.Execute($"ALTER TABLE calculation_punches DROP COLUMN {column}");
        }

        connection.Execute("DROP TABLE users");
    }

    private static IReadOnlyList<string> Versions(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_migrations ORDER BY version";
        using SqliteDataReader reader = command.ExecuteReader();
        var versions = new List<string>();
        while (reader.Read())
        {
            versions.Add(reader.GetString(0));
        }

        return versions;
    }

    private static IReadOnlyList<string> Columns(SqliteConnection connection, string table)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }

    private static IReadOnlyList<object[]> Rows(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        using SqliteDataReader reader = command.ExecuteReader();
        var rows = new List<object[]>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(values);
        }

        return rows;
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static long Number(SqliteConnection connection, string sql)
    {
        object? value = Scalar(connection, sql);
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}

file static class SqliteTestExtensions
{
    public static void Execute(this SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
