using Atten.Core;
using Microsoft.Data.Sqlite;

namespace Atten.Data;

internal sealed class SqliteLeaveRepository : ILeaveRepository
{
    private readonly SqliteSession _session;

    public SqliteLeaveRepository(SqliteSession session)
    {
        _session = session;
    }

    public long Add(long personnelId, DateOnly startDate, DateOnly endDate, int minutes)
    {
        long personId = RepositoryGuard.PersonId(personnelId);
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        int amount = RepositoryGuard.LeaveMinutes(minutes);
        string createdAt = _session.Timestamp();
        return _session.Write(
            connection =>
            {
                RepositoryGuard.RequirePersonnel(connection, transaction: null, personId);
                RejectIfLeaveExceeds(connection, personId, start, amount, excludeId: null);
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO leaves (
                        personnel_id, start_date, end_date, minutes, created_at, updated_at,
                        created_by, updated_by
                    )
                    VALUES (
                        $personnel_id, $start_date, $end_date, $minutes, $created_at, $updated_at,
                        $created_by, $updated_by
                    )
                    """;
                command.Parameters.AddWithValue("$personnel_id", personId);
                command.Parameters.AddWithValue("$start_date", start);
                command.Parameters.AddWithValue("$end_date", end);
                command.Parameters.AddWithValue("$minutes", amount);
                command.Parameters.AddWithValue("$created_at", createdAt);
                command.Parameters.AddWithValue("$updated_at", createdAt);
                command.Parameters.AddWithValue("$created_by", _session.ActorId);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.ExecuteNonQuery();
                return SqliteReader.LastInsertId(connection);
            },
            RepositoryGuard.LeaveSaveError);
    }

    public void Update(long leaveId, long personnelId, DateOnly startDate, DateOnly endDate, int minutes)
    {
        long leaveKey = RepositoryGuard.LeaveId(leaveId);
        long personId = RepositoryGuard.PersonId(personnelId);
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        int amount = RepositoryGuard.LeaveMinutes(minutes);
        string updatedAt = _session.Timestamp();
        _session.Write(
            connection =>
            {
                using SqliteCommand found = connection.CreateCommand();
                found.CommandText = "SELECT 1 FROM leaves WHERE id = $id";
                found.Parameters.AddWithValue("$id", leaveKey);
                if (found.ExecuteScalar() is null)
                {
                    throw new ArgumentException(RepositoryGuard.MissingLeave);
                }

                RepositoryGuard.RequirePersonnel(connection, transaction: null, personId);
                RejectIfLeaveExceeds(connection, personId, start, amount, leaveKey);
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE leaves
                    SET personnel_id = $personnel_id,
                        start_date = $start_date,
                        end_date = $end_date,
                        minutes = $minutes,
                        updated_at = $updated_at,
                        updated_by = $updated_by
                    WHERE id = $id
                    """;
                command.Parameters.AddWithValue("$personnel_id", personId);
                command.Parameters.AddWithValue("$start_date", start);
                command.Parameters.AddWithValue("$end_date", end);
                command.Parameters.AddWithValue("$minutes", amount);
                command.Parameters.AddWithValue("$updated_at", updatedAt);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.Parameters.AddWithValue("$id", leaveKey);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.LeaveSaveError);
    }

    public IReadOnlyList<LeaveRecord> List(long personnelId, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        long personId = RepositoryGuard.PersonId(personnelId);
        string? start = startDate is null ? null : RepositoryGuard.StorageDay(startDate.Value);
        string? end = endDate is null ? null : RepositoryGuard.StorageDay(endDate.Value);
        if (start is not null && end is not null && string.CompareOrdinal(start, end) > 0)
        {
            throw new ArgumentException(RepositoryGuard.InvalidRange);
        }

        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            var clauses = new List<string> { "personnel_id = $personnel_id" };
            command.Parameters.AddWithValue("$personnel_id", personId);
            if (start is not null)
            {
                clauses.Add("end_date >= $start");
                command.Parameters.AddWithValue("$start", start);
            }

            if (end is not null)
            {
                clauses.Add("start_date <= $end");
                command.Parameters.AddWithValue("$end", end);
            }

            command.CommandText =
                $"""
                SELECT id, personnel_id, start_date, end_date, minutes, created_at, updated_at,
                       created_by, updated_by
                FROM leaves
                WHERE {string.Join(" AND ", clauses)}
                ORDER BY start_date, id
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            var rows = new List<LeaveRecord>();
            while (reader.Read())
            {
                rows.Add(new LeaveRecord(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    (int)reader.GetInt64(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    SqliteReader.Int64(reader, 7),
                    SqliteReader.Int64(reader, 8)));
            }

            return rows;
        });
    }

    public int SumMinutes(long personnelId, DateOnly startDate, DateOnly endDate)
    {
        long personId = RepositoryGuard.PersonId(personnelId);
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COALESCE(SUM(minutes), 0)
                FROM leaves
                WHERE personnel_id = $personnel_id AND start_date >= $start AND start_date <= $end
                """;
            command.Parameters.AddWithValue("$personnel_id", personId);
            command.Parameters.AddWithValue("$start", start);
            command.Parameters.AddWithValue("$end", end);
            return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        });
    }

    private static void RejectIfLeaveExceeds(
        SqliteConnection connection,
        long personId,
        string start,
        int amount,
        long? excludeId)
    {
        using SqliteCommand person = connection.CreateCommand();
        person.CommandText = "SELECT cooperation_start, cooperation_end FROM personnel WHERE id = $id";
        person.Parameters.AddWithValue("$id", personId);
        using SqliteDataReader reader = person.ExecuteReader();
        if (!reader.Read())
        {
            throw new ArgumentException(RepositoryGuard.MissingPerson);
        }

        DateOnly? cooperationStart = Leave.OptionalDate(SqliteReader.Text(reader, 0));
        DateOnly? cooperationEnd = Leave.OptionalDate(SqliteReader.Text(reader, 1));
        reader.Close();

        int year = Leave.ShamsiYearOf(start);
        int remaining = Leave.RemainingLeaveMinutes(
            year,
            cooperationStart,
            cooperationEnd,
            UsedLeaveMinutesByYear(connection, personId, excludeId));
        if (amount > remaining)
        {
            string left = Leave.FormatLeaveAmount(Math.Max(remaining, 0));
            throw new ArgumentException($"این مرخصی از مانده سال {year} بیشتر است. باقی‌مانده: {left}");
        }
    }

    private static Dictionary<int, int> UsedLeaveMinutesByYear(
        SqliteConnection connection,
        long personId,
        long? excludeId)
    {
        using SqliteCommand command = connection.CreateCommand();
        string exclude = "";
        if (excludeId is not null)
        {
            exclude = " AND id != $exclude_id";
            command.Parameters.AddWithValue("$exclude_id", excludeId.Value);
        }

        command.CommandText =
            $"""
            SELECT start_date, minutes
            FROM leaves
            WHERE personnel_id = $personnel_id{exclude}
            """;
        command.Parameters.AddWithValue("$personnel_id", personId);
        using SqliteDataReader reader = command.ExecuteReader();
        var used = new Dictionary<int, int>();
        while (reader.Read())
        {
            int year = Leave.ShamsiYearOf(reader.GetString(0));
            used[year] = used.GetValueOrDefault(year) + (int)reader.GetInt64(1);
        }

        return used;
    }
}
