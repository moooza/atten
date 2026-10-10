using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Atten.Data;

/// <summary>
/// Apply numbered SQL migrations that have not been recorded yet.
/// Versions 001–011 are the original schema. 012 adds users and actor columns.
/// 013 adds payroll runs with the same created_by / updated_by columns.
/// </summary>
public static class Migrator
{
    private const string ResourcePrefix = "Atten.Data.Migrations.";

    public static IReadOnlyList<string> Apply()
    {
        return Apply(AppPaths.DbFile());
    }

    public static IReadOnlyList<string> Apply(string dbFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);

        var appliedNow = new List<string>();
        using SqliteConnection connection = SqliteDatabase.Open(dbFile);
        using SqliteTransaction transaction = connection.BeginTransaction();
        HashSet<string> done = AppliedVersions(connection, transaction);
        foreach ((string version, string sql) in MigrationScripts())
        {
            if (done.Contains(version))
            {
                continue;
            }

            ExecuteScript(connection, transaction, sql);
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO schema_migrations (version, applied_at) VALUES ($version, $applied_at)";
            insert.Parameters.AddWithValue("$version", version);
            insert.Parameters.AddWithValue("$applied_at", UtcNowIso());
            insert.ExecuteNonQuery();
            appliedNow.Add(version);
        }

        transaction.Commit();
        return appliedNow;
    }

    internal static IReadOnlyList<(string Version, string FileName, string Sql)> EmbeddedMigrations()
    {
        Assembly assembly = typeof(Migrator).Assembly;
        var found = new List<(string Version, string FileName, string Sql)>();
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                !name.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            string fileName = name[ResourcePrefix.Length..];
            string version = fileName.Split('_', 2)[0];
            using Stream stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing migration resource {name}.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            found.Add((version, fileName, reader.ReadToEnd()));
        }

        found.Sort((left, right) => string.CompareOrdinal(left.FileName, right.FileName));
        return found;
    }

    internal static IReadOnlyList<(string Version, string Sql)> MigrationScripts()
    {
        return EmbeddedMigrations()
            .Select(item => (item.Version, item.Sql))
            .ToList();
    }

    private static HashSet<string> AppliedVersions(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations'";
        if (exists.ExecuteScalar() is null)
        {
            return [];
        }

        using SqliteCommand select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT version FROM schema_migrations";
        using SqliteDataReader reader = select.ExecuteReader();
        var versions = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            versions.Add(reader.GetString(0));
        }

        return versions;
    }

    private static void ExecuteScript(SqliteConnection connection, SqliteTransaction transaction, string script)
    {
        foreach (string statement in SplitStatements(script))
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    internal static IEnumerable<string> SplitStatements(string script)
    {
        var builder = new StringBuilder();
        using var reader = new StringReader(script);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            builder.AppendLine(line);
        }

        foreach (string part in builder.ToString().Split(';'))
        {
            string statement = part.Trim();
            if (statement.Length > 0)
            {
                yield return statement;
            }
        }
    }

    private static string UtcNowIso()
    {
        return DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz", CultureInfo.InvariantCulture);
    }
}
