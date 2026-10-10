using Microsoft.Data.Sqlite;

namespace Atten.Data;

internal sealed class SqliteUserRepository : IUserRepository
{
    private readonly SqliteSession _session;

    public SqliteUserRepository(SqliteSession session)
    {
        _session = session;
    }

    public UserRecord? Get(long userId)
    {
        if (userId <= 0)
        {
            return null;
        }

        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT id, display_name, created_at FROM users WHERE id = $id";
            command.Parameters.AddWithValue("$id", userId);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        });
    }

    public IReadOnlyList<UserRecord> List()
    {
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT id, display_name, created_at FROM users ORDER BY id";
            using SqliteDataReader reader = command.ExecuteReader();
            var rows = new List<UserRecord>();
            while (reader.Read())
            {
                rows.Add(Map(reader));
            }

            return rows;
        });
    }

    private static UserRecord Map(SqliteDataReader reader)
    {
        return new UserRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2));
    }
}
