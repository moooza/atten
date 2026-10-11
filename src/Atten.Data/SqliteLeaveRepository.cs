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

    public long Add(long personnelId, DateOnly startDate, DateOnly endDate, int minutes, string? description = null)
    {
        long personId = RepositoryGuard.PersonId(personnelId);
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        int amount = RepositoryGuard.LeaveMinutes(minutes);
        string? note = RepositoryGuard.BlankToNone(description);
        string createdAt = _session.Timestamp();
        return _session.Write(
            connection =>
            {
                RepositoryGuard.RequirePersonnel(connection, transaction: null, personId);
                RejectIfNoWorkingDays(connection, startDate, endDate);
                RejectIfOverlapsWorkingDays(connection, personId, startDate, endDate, excludeId: null);
                RejectIfLeaveExceeds(connection, personId, start, amount, excludeId: null);
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO leaves (
                        personnel_id, start_date, end_date, minutes, kind, description, created_at, updated_at,
                        created_by, updated_by
                    )
                    VALUES (
                        $personnel_id, $start_date, $end_date, $minutes, $kind, $description, $created_at, $updated_at,
                        $created_by, $updated_by
                    )
                    """;
                command.Parameters.AddWithValue("$personnel_id", personId);
                command.Parameters.AddWithValue("$start_date", start);
                command.Parameters.AddWithValue("$end_date", end);
                command.Parameters.AddWithValue("$minutes", amount);
                command.Parameters.AddWithValue("$kind", Leave.KindLeave);
                command.Parameters.AddWithValue("$description", RepositoryGuard.Db(note));
                command.Parameters.AddWithValue("$created_at", createdAt);
                command.Parameters.AddWithValue("$updated_at", createdAt);
                command.Parameters.AddWithValue("$created_by", _session.ActorId);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.ExecuteNonQuery();
                return SqliteReader.LastInsertId(connection);
            },
            RepositoryGuard.LeaveSaveError);
    }

    public void Update(long leaveId, long personnelId, DateOnly startDate, DateOnly endDate, int minutes, string? description = null)
    {
        long leaveKey = RepositoryGuard.LeaveId(leaveId);
        long personId = RepositoryGuard.PersonId(personnelId);
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        int amount = RepositoryGuard.LeaveMinutes(minutes);
        string? note = RepositoryGuard.BlankToNone(description);
        string updatedAt = _session.Timestamp();
        _session.Write(
            connection =>
            {
                using SqliteCommand found = connection.CreateCommand();
                found.CommandText = "SELECT kind FROM leaves WHERE id = $id";
                found.Parameters.AddWithValue("$id", leaveKey);
                object? kind = found.ExecuteScalar();
                if (kind is null)
                {
                    throw new ArgumentException(RepositoryGuard.MissingLeave);
                }

                if (string.Equals(Convert.ToString(kind, System.Globalization.CultureInfo.InvariantCulture), Leave.KindSettlement, StringComparison.Ordinal))
                {
                    throw new ArgumentException(RepositoryGuard.LeaveSettlementEdit);
                }

                RepositoryGuard.RequirePersonnel(connection, transaction: null, personId);
                RejectIfNoWorkingDays(connection, startDate, endDate);
                RejectIfOverlapsWorkingDays(connection, personId, startDate, endDate, leaveKey);
                RejectIfLeaveExceeds(connection, personId, start, amount, leaveKey);
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE leaves
                    SET personnel_id = $personnel_id,
                        start_date = $start_date,
                        end_date = $end_date,
                        minutes = $minutes,
                        kind = $kind,
                        description = $description,
                        updated_at = $updated_at,
                        updated_by = $updated_by
                    WHERE id = $id
                    """;
                command.Parameters.AddWithValue("$personnel_id", personId);
                command.Parameters.AddWithValue("$start_date", start);
                command.Parameters.AddWithValue("$end_date", end);
                command.Parameters.AddWithValue("$minutes", amount);
                command.Parameters.AddWithValue("$kind", Leave.KindLeave);
                command.Parameters.AddWithValue("$description", RepositoryGuard.Db(note));
                command.Parameters.AddWithValue("$updated_at", updatedAt);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.Parameters.AddWithValue("$id", leaveKey);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.LeaveSaveError);
    }

    public void Delete(long leaveId)
    {
        long leaveKey = RepositoryGuard.LeaveId(leaveId);
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

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM leaves WHERE id = $id";
                command.Parameters.AddWithValue("$id", leaveKey);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.LeaveDeleteError);
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
                SELECT id, personnel_id, start_date, end_date, minutes, kind, description, created_at, updated_at,
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
                    SqliteReader.Text(reader, 6),
                    reader.GetString(7),
                    reader.GetString(8),
                    SqliteReader.Int64(reader, 9),
                    SqliteReader.Int64(reader, 10)));
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

    public IReadOnlyDictionary<int, int> CarryMinutesByYear(long personnelId)
    {
        long personId = RepositoryGuard.PersonId(personnelId);
        return _session.Read(connection => CarryMinutesByYear(connection, personId));
    }

    public void ApplyLeftover(long personnelId, int year, int transferMinutes, int settleMinutes)
    {
        long personId = RepositoryGuard.PersonId(personnelId);
        int shamsiYear = RepositoryGuard.LeaveYear(year);
        string createdAt = _session.Timestamp();
        _session.Write(
            connection =>
            {
                LeftoverState state = LoadLeftover(connection, personId, shamsiYear);
                Leave.ValidateLeftoverAllocation(
                    shamsiYear,
                    state.Balance.RemainingBeforeTransfer,
                    transferMinutes,
                    settleMinutes,
                    state.ClosingYear);
                if (settleMinutes > 0)
                {
                    DateOnly lastDay = Leave.ShamsiYearSpan(shamsiYear).End;
                    string day = RepositoryGuard.StorageDay(lastDay);
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText =
                        """
                        INSERT INTO leaves (
                            personnel_id, start_date, end_date, minutes, kind, created_at, updated_at,
                            created_by, updated_by
                        )
                        VALUES (
                            $personnel_id, $start_date, $end_date, $minutes, $kind, $created_at, $updated_at,
                            $created_by, $updated_by
                        )
                        """;
                    command.Parameters.AddWithValue("$personnel_id", personId);
                    command.Parameters.AddWithValue("$start_date", day);
                    command.Parameters.AddWithValue("$end_date", day);
                    command.Parameters.AddWithValue("$minutes", settleMinutes);
                    command.Parameters.AddWithValue("$kind", Leave.KindSettlement);
                    command.Parameters.AddWithValue("$created_at", createdAt);
                    command.Parameters.AddWithValue("$updated_at", createdAt);
                    command.Parameters.AddWithValue("$created_by", _session.ActorId);
                    command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                    command.ExecuteNonQuery();
                }

                if (transferMinutes > 0)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText =
                        """
                        INSERT INTO leave_year_transfers (
                            personnel_id, year, carry_minutes, created_at, updated_at,
                            created_by, updated_by
                        )
                        VALUES (
                            $personnel_id, $year, $carry_minutes, $created_at, $updated_at,
                            $created_by, $updated_by
                        )
                        ON CONFLICT(personnel_id, year) DO UPDATE SET
                            carry_minutes = excluded.carry_minutes,
                            updated_at = excluded.updated_at,
                            updated_by = excluded.updated_by
                        """;
                    command.Parameters.AddWithValue("$personnel_id", personId);
                    command.Parameters.AddWithValue("$year", shamsiYear);
                    command.Parameters.AddWithValue("$carry_minutes", transferMinutes);
                    command.Parameters.AddWithValue("$created_at", createdAt);
                    command.Parameters.AddWithValue("$updated_at", createdAt);
                    command.Parameters.AddWithValue("$created_by", _session.ActorId);
                    command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                    command.ExecuteNonQuery();
                }
            },
            RepositoryGuard.LeaveSaveError);
    }

    private static void RejectIfNoWorkingDays(
        SqliteConnection connection,
        DateOnly startDate,
        DateOnly endDate)
    {
        IReadOnlySet<string> official = OfficialHolidayCatalog(connection, startDate, endDate);
        bool Holiday(DateOnly day) => OfficialHoliday.IsHoliday(day, official);
        int? working = Leave.WorkingDayCount(startDate, endDate, Holiday);
        if (working == 0 || (working is null && Holiday(startDate)))
        {
            throw new ArgumentException(RepositoryGuard.LeaveNoWorkingDays);
        }
    }

    private static IReadOnlySet<string> OfficialHolidayCatalog(
        SqliteConnection connection,
        DateOnly startDate,
        DateOnly endDate)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT date
            FROM official_holidays
            WHERE date >= $start AND date <= $end
            """;
        command.Parameters.AddWithValue("$start", Dates.StorageDate(startDate));
        command.Parameters.AddWithValue("$end", Dates.StorageDate(endDate));
        using SqliteDataReader reader = command.ExecuteReader();
        var dates = new List<string>();
        while (reader.Read())
        {
            dates.Add(reader.GetString(0));
        }

        return OfficialHoliday.Catalog(dates);
    }

    private static void RejectIfOverlapsWorkingDays(
        SqliteConnection connection,
        long personId,
        DateOnly startDate,
        DateOnly endDate,
        long? excludeId)
    {
        IReadOnlySet<string> official = OfficialHolidayCatalog(connection, startDate, endDate);
        bool Holiday(DateOnly day) => OfficialHoliday.IsHoliday(day, official);

        using SqliteCommand command = connection.CreateCommand();
        string exclude = "";
        if (excludeId is not null)
        {
            exclude = " AND id != $exclude_id";
            command.Parameters.AddWithValue("$exclude_id", excludeId.Value);
        }

        command.CommandText =
            $"""
            SELECT start_date, end_date
            FROM leaves
            WHERE personnel_id = $personnel_id
              AND kind = $kind
              AND start_date <= $end
              AND end_date >= $start{exclude}
            """;
        command.Parameters.AddWithValue("$personnel_id", personId);
        command.Parameters.AddWithValue("$kind", Leave.KindLeave);
        command.Parameters.AddWithValue("$start", Dates.StorageDate(startDate));
        command.Parameters.AddWithValue("$end", Dates.StorageDate(endDate));
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            DateOnly otherStart = RepositoryGuard.ParseStorageDay(reader.GetString(0));
            DateOnly otherEnd = RepositoryGuard.ParseStorageDay(reader.GetString(1));
            if (Leave.OverlapsWorkingDays(startDate, endDate, otherStart, otherEnd, Holiday))
            {
                throw new ArgumentException(RepositoryGuard.LeaveOverlapsWorkingDay);
            }
        }
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
            UsedLeaveMinutesByYear(connection, personId, excludeId),
            CarryMinutesByYear(connection, personId));
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

    private static Dictionary<int, int> CarryMinutesByYear(SqliteConnection connection, long personId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT year, carry_minutes
            FROM leave_year_transfers
            WHERE personnel_id = $personnel_id
            """;
        command.Parameters.AddWithValue("$personnel_id", personId);
        using SqliteDataReader reader = command.ExecuteReader();
        var caps = new Dictionary<int, int>();
        while (reader.Read())
        {
            caps[(int)reader.GetInt64(0)] = (int)reader.GetInt64(1);
        }

        return caps;
    }

    private static LeftoverState LoadLeftover(SqliteConnection connection, long personId, int year)
    {
        RepositoryGuard.RequirePersonnel(connection, transaction: null, personId);
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

        Dictionary<int, int> usedByYear = UsedLeaveMinutesByYear(connection, personId, excludeId: null);
        Dictionary<int, int> carryCaps = CarryMinutesByYear(connection, personId);
        IReadOnlyDictionary<int, LeaveYearSettlement> settled = Leave.SettleCooperationYears(
            cooperationStart,
            cooperationEnd,
            usedByYear,
            carryCaps: carryCaps);
        int? closingYear = cooperationEnd is null ? null : Leave.ShamsiYearOf(cooperationEnd.Value);
        return new LeftoverState(
            closingYear,
            Leave.YearBalance(year, cooperationStart, settled, usedByYear));
    }

    private sealed record LeftoverState(int? ClosingYear, LeaveYearBalance Balance);
}
