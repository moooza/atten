using Microsoft.Data.Sqlite;

namespace Atten.Data;

/// <summary>
/// SQLite connection settings for the portable database file.
/// WAL with synchronous=FULL keeps a finished save in a side file that SQLite
/// replays after power loss. AUTOINCREMENT, PRAGMA, and engine date functions
/// stay here.
/// </summary>
internal static class SqliteDatabase
{
    public const int BusyTimeoutMilliseconds = 5000;

    public static SqliteConnection Open(string dbFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);

        string fullPath = Path.GetFullPath(dbFile);
        string? parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            DefaultTimeout = BusyTimeoutMilliseconds / 1000,
            Pooling = false,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        try
        {
            ApplyPragmas(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return connection;
    }

    public static void Use(string dbFile, Action<SqliteConnection> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using SqliteConnection connection = Open(dbFile);
        action(connection);
    }

    public static T Use<T>(string dbFile, Func<SqliteConnection, T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        using SqliteConnection connection = Open(dbFile);
        return func(connection);
    }

    /// <summary>
    /// Open a file without WAL or foreign-key pragmas. Used to inspect a copy
    /// or a possibly damaged file without changing how it is stored.
    /// </summary>
    public static SqliteConnection OpenRaw(string dbFile, SqliteOpenMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);

        string fullPath = Path.GetFullPath(dbFile);
        string? parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = mode,
            ForeignKeys = false,
            DefaultTimeout = BusyTimeoutMilliseconds / 1000,
            Pooling = false,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        try
        {
            Execute(connection, $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds}");
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return connection;
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        Execute(connection, $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds}");
        Execute(connection, "PRAGMA journal_mode = WAL");
        Execute(connection, "PRAGMA synchronous = FULL");
        Execute(connection, "PRAGMA foreign_keys = ON");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
