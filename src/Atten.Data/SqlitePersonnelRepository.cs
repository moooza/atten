using Microsoft.Data.Sqlite;

namespace Atten.Data;

internal sealed class SqlitePersonnelRepository : IPersonnelRepository
{
    private readonly SqliteSession _session;

    public SqlitePersonnelRepository(SqliteSession session)
    {
        _session = session;
    }

    public long Add(
        string firstName,
        string lastName,
        double dailyHours,
        string? remoteId = null,
        string? mobile = null,
        DateOnly? cooperationStart = null,
        DateOnly? cooperationEnd = null)
    {
        var fields = RepositoryGuard.PersonnelFields(
            firstName,
            lastName,
            dailyHours,
            remoteId,
            mobile,
            cooperationStart,
            cooperationEnd);
        string createdAt = _session.Timestamp();
        return _session.Write(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO personnel (
                        remote_id, first_name, last_name, daily_hours, mobile,
                        cooperation_start, cooperation_end, created_at, updated_at,
                        created_by, updated_by
                    )
                    VALUES (
                        $remote_id, $first_name, $last_name, $daily_hours, $mobile,
                        $cooperation_start, $cooperation_end, $created_at, $updated_at,
                        $created_by, $updated_by
                    )
                    """;
                BindFields(command, fields, createdAt, createdAt, _session.ActorId, _session.ActorId);
                command.ExecuteNonQuery();
                return SqliteReader.LastInsertId(connection);
            },
            RepositoryGuard.PersonnelSaveError);
    }

    public void Update(
        long personId,
        string firstName,
        string lastName,
        double dailyHours,
        string? remoteId = null,
        string? mobile = null,
        DateOnly? cooperationStart = null,
        DateOnly? cooperationEnd = null)
    {
        long id = RepositoryGuard.PersonId(personId);
        var fields = RepositoryGuard.PersonnelFields(
            firstName,
            lastName,
            dailyHours,
            remoteId,
            mobile,
            cooperationStart,
            cooperationEnd);
        string updatedAt = _session.Timestamp();
        _session.Write(
            connection =>
            {
                RepositoryGuard.RequirePersonnel(connection, transaction: null, id);
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE personnel
                    SET remote_id = $remote_id,
                        first_name = $first_name,
                        last_name = $last_name,
                        daily_hours = $daily_hours,
                        mobile = $mobile,
                        cooperation_start = $cooperation_start,
                        cooperation_end = $cooperation_end,
                        updated_at = $updated_at,
                        updated_by = $updated_by
                    WHERE id = $id
                    """;
                BindFields(command, fields, createdAt: null, updatedAt, createdBy: null, _session.ActorId);
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.PersonnelSaveError);
    }

    public IReadOnlyList<PersonnelRecord> List()
    {
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, remote_id, first_name, last_name, daily_hours, mobile,
                       cooperation_start, cooperation_end, created_at, updated_at,
                       created_by, updated_by
                FROM personnel
                ORDER BY id
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            var rows = new List<PersonnelRecord>();
            while (reader.Read())
            {
                rows.Add(new PersonnelRecord(
                    reader.GetInt64(0),
                    SqliteReader.Text(reader, 1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDouble(4),
                    SqliteReader.Text(reader, 5),
                    SqliteReader.Text(reader, 6),
                    SqliteReader.Text(reader, 7),
                    SqliteReader.Text(reader, 8),
                    SqliteReader.Text(reader, 9),
                    SqliteReader.Int64(reader, 10),
                    SqliteReader.Int64(reader, 11)));
            }

            return rows;
        });
    }

    private static void BindFields(
        SqliteCommand command,
        (string? RemoteId, string FirstName, string LastName, double Hours, string? Mobile, string? Started, string? Ended) fields,
        string? createdAt,
        string updatedAt,
        long? createdBy,
        long updatedBy)
    {
        command.Parameters.AddWithValue("$remote_id", RepositoryGuard.Db(fields.RemoteId));
        command.Parameters.AddWithValue("$first_name", fields.FirstName);
        command.Parameters.AddWithValue("$last_name", fields.LastName);
        command.Parameters.AddWithValue("$daily_hours", fields.Hours);
        command.Parameters.AddWithValue("$mobile", RepositoryGuard.Db(fields.Mobile));
        command.Parameters.AddWithValue("$cooperation_start", RepositoryGuard.Db(fields.Started));
        command.Parameters.AddWithValue("$cooperation_end", RepositoryGuard.Db(fields.Ended));
        command.Parameters.AddWithValue("$updated_at", updatedAt);
        command.Parameters.AddWithValue("$updated_by", updatedBy);
        if (createdAt is not null)
        {
            command.Parameters.AddWithValue("$created_at", createdAt);
        }

        if (createdBy is not null)
        {
            command.Parameters.AddWithValue("$created_by", createdBy.Value);
        }
    }
}
