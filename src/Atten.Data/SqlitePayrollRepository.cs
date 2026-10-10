using Atten.Core;
using Microsoft.Data.Sqlite;

namespace Atten.Data;

internal sealed class SqlitePayrollRepository : IPayrollRepository
{
    private readonly SqliteSession _session;

    public SqlitePayrollRepository(SqliteSession session)
    {
        _session = session;
    }

    public PayrollRunRecord Draft(long personnelId, DateOnly startDate, DateOnly endDate)
    {
        long personId = RepositoryGuard.PersonId(personnelId);
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        string stamp = _session.Timestamp();
        return _session.Write(
            connection =>
            {
                PersonSnapshot person = LoadPerson(connection, personId);
                PayrollTotals totals = Snapshot(connection, person, start, end, startDate);
                using SqliteCommand found = connection.CreateCommand();
                found.CommandText =
                    """
                    SELECT id, status
                    FROM payroll_runs
                    WHERE personnel_id = $personnel_id AND start_date = $start AND end_date = $end
                    """;
                found.Parameters.AddWithValue("$personnel_id", personId);
                found.Parameters.AddWithValue("$start", start);
                found.Parameters.AddWithValue("$end", end);
                using SqliteDataReader reader = found.ExecuteReader();
                if (reader.Read())
                {
                    long existingId = reader.GetInt64(0);
                    string status = reader.GetString(1);
                    reader.Close();
                    Payroll.EnsureCanEdit(status);
                    UpdateTotals(connection, existingId, totals, stamp, _session.ActorId);
                    return ReadRequired(connection, existingId);
                }

                reader.Close();
                using SqliteCommand insert = connection.CreateCommand();
                insert.CommandText =
                    """
                    INSERT INTO payroll_runs (
                        personnel_id, start_date, end_date, overtime_minutes, deficit_minutes,
                        leave_minutes, remaining_leave_minutes, status, created_at, updated_at,
                        created_by, updated_by
                    )
                    VALUES (
                        $personnel_id, $start_date, $end_date, $overtime_minutes, $deficit_minutes,
                        $leave_minutes, $remaining_leave_minutes, $status, $created_at, $updated_at,
                        $created_by, $updated_by
                    )
                    """;
                insert.Parameters.AddWithValue("$personnel_id", personId);
                insert.Parameters.AddWithValue("$start_date", start);
                insert.Parameters.AddWithValue("$end_date", end);
                insert.Parameters.AddWithValue("$overtime_minutes", totals.OvertimeMinutes);
                insert.Parameters.AddWithValue("$deficit_minutes", totals.DeficitMinutes);
                insert.Parameters.AddWithValue("$leave_minutes", totals.LeaveMinutes);
                insert.Parameters.AddWithValue("$remaining_leave_minutes", totals.RemainingLeaveMinutes);
                insert.Parameters.AddWithValue("$status", Payroll.Draft);
                insert.Parameters.AddWithValue("$created_at", stamp);
                insert.Parameters.AddWithValue("$updated_at", stamp);
                insert.Parameters.AddWithValue("$created_by", _session.ActorId);
                insert.Parameters.AddWithValue("$updated_by", _session.ActorId);
                insert.ExecuteNonQuery();
                return ReadRequired(connection, SqliteReader.LastInsertId(connection));
            },
            RepositoryGuard.PayrollSaveError);
    }

    public PayrollRunRecord Recalculate(long payrollId)
    {
        PayrollRunRecord row = Get(payrollId) ?? throw new ArgumentException(RepositoryGuard.MissingPayroll);
        Payroll.EnsureCanEdit(row.Status);
        return Draft(
            row.PersonnelId,
            RepositoryGuard.ParseStorageDay(row.StartDate),
            RepositoryGuard.ParseStorageDay(row.EndDate));
    }

    public void Confirm(long payrollId)
    {
        SetStatus(payrollId, Payroll.Confirmed, Payroll.EnsureCanConfirm);
    }

    public void Reopen(long payrollId)
    {
        SetStatus(payrollId, Payroll.Draft, Payroll.EnsureCanReopen);
    }

    public PayrollRunRecord? Get(long payrollId)
    {
        long id = RepositoryGuard.PayrollId(payrollId);
        return _session.Read(connection => Read(connection, id));
    }

    public IReadOnlyList<PayrollRunRecord> List(long? personnelId = null)
    {
        long? personId = personnelId is null ? null : RepositoryGuard.PersonId(personnelId.Value);
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            var clauses = new List<string>();
            if (personId is not null)
            {
                clauses.Add("personnel_id = $personnel_id");
                command.Parameters.AddWithValue("$personnel_id", personId.Value);
            }

            string where = clauses.Count == 0 ? "" : $"WHERE {string.Join(" AND ", clauses)}";
            command.CommandText =
                $"""
                SELECT {SelectColumns}
                FROM payroll_runs
                {where}
                ORDER BY start_date DESC, id DESC
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            var rows = new List<PayrollRunRecord>();
            while (reader.Read())
            {
                rows.Add(Map(reader));
            }

            return rows;
        });
    }

    private void SetStatus(long payrollId, string status, Action<string> ensure)
    {
        long id = RepositoryGuard.PayrollId(payrollId);
        string stamp = _session.Timestamp();
        _session.Write(
            connection =>
            {
                using SqliteCommand found = connection.CreateCommand();
                found.CommandText = "SELECT status FROM payroll_runs WHERE id = $id";
                found.Parameters.AddWithValue("$id", id);
                object? current = found.ExecuteScalar();
                if (current is null)
                {
                    throw new ArgumentException(RepositoryGuard.MissingPayroll);
                }

                ensure(Convert.ToString(current, System.Globalization.CultureInfo.InvariantCulture) ?? "");
                using SqliteCommand update = connection.CreateCommand();
                update.CommandText =
                    """
                    UPDATE payroll_runs
                    SET status = $status,
                        updated_at = $updated_at,
                        updated_by = $updated_by
                    WHERE id = $id
                    """;
                update.Parameters.AddWithValue("$status", status);
                update.Parameters.AddWithValue("$updated_at", stamp);
                update.Parameters.AddWithValue("$updated_by", _session.ActorId);
                update.Parameters.AddWithValue("$id", id);
                update.ExecuteNonQuery();
            },
            RepositoryGuard.PayrollSaveError);
    }

    private static void UpdateTotals(
        SqliteConnection connection,
        long payrollId,
        PayrollTotals totals,
        string stamp,
        long actorId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE payroll_runs
            SET overtime_minutes = $overtime_minutes,
                deficit_minutes = $deficit_minutes,
                leave_minutes = $leave_minutes,
                remaining_leave_minutes = $remaining_leave_minutes,
                updated_at = $updated_at,
                updated_by = $updated_by
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$overtime_minutes", totals.OvertimeMinutes);
        command.Parameters.AddWithValue("$deficit_minutes", totals.DeficitMinutes);
        command.Parameters.AddWithValue("$leave_minutes", totals.LeaveMinutes);
        command.Parameters.AddWithValue("$remaining_leave_minutes", totals.RemainingLeaveMinutes);
        command.Parameters.AddWithValue("$updated_at", stamp);
        command.Parameters.AddWithValue("$updated_by", actorId);
        command.Parameters.AddWithValue("$id", payrollId);
        command.ExecuteNonQuery();
    }

    private static PayrollTotals Snapshot(
        SqliteConnection connection,
        PersonSnapshot person,
        string start,
        string end,
        DateOnly startDate)
    {
        var balances = new List<int>();
        if (person.RemoteId is not null)
        {
            using SqliteCommand days = connection.CreateCommand();
            days.CommandText =
                """
                SELECT balance_minutes
                FROM calculation_days
                WHERE remote_id = $remote_id AND date >= $start AND date <= $end
                """;
            days.Parameters.AddWithValue("$remote_id", person.RemoteId);
            days.Parameters.AddWithValue("$start", start);
            days.Parameters.AddWithValue("$end", end);
            using SqliteDataReader reader = days.ExecuteReader();
            while (reader.Read())
            {
                balances.Add((int)reader.GetInt64(0));
            }
        }

        using SqliteCommand leave = connection.CreateCommand();
        leave.CommandText =
            """
            SELECT COALESCE(SUM(minutes), 0)
            FROM leaves
            WHERE personnel_id = $personnel_id AND start_date >= $start AND start_date <= $end
            """;
        leave.Parameters.AddWithValue("$personnel_id", person.Id);
        leave.Parameters.AddWithValue("$start", start);
        leave.Parameters.AddWithValue("$end", end);
        int leaveMinutes = Convert.ToInt32(leave.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        int remaining = Leave.RemainingLeaveMinutes(
            Leave.ShamsiYearOf(startDate),
            person.CooperationStart,
            person.CooperationEnd,
            UsedLeaveMinutesByYear(connection, person.Id));
        return Payroll.Summarize(balances, leaveMinutes, remaining);
    }

    private static Dictionary<int, int> UsedLeaveMinutesByYear(SqliteConnection connection, long personId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT start_date, minutes FROM leaves WHERE personnel_id = $personnel_id";
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

    private static PersonSnapshot LoadPerson(SqliteConnection connection, long personId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, remote_id, cooperation_start, cooperation_end FROM personnel WHERE id = $id";
        command.Parameters.AddWithValue("$id", personId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new ArgumentException(RepositoryGuard.MissingPerson);
        }

        return new PersonSnapshot(
            reader.GetInt64(0),
            SqliteReader.Text(reader, 1),
            Leave.OptionalDate(SqliteReader.Text(reader, 2)),
            Leave.OptionalDate(SqliteReader.Text(reader, 3)));
    }

    private static PayrollRunRecord ReadRequired(SqliteConnection connection, long payrollId)
    {
        return Read(connection, payrollId) ?? throw new ArgumentException(RepositoryGuard.MissingPayroll);
    }

    private static PayrollRunRecord? Read(SqliteConnection connection, long payrollId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT {SelectColumns}
            FROM payroll_runs
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", payrollId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    private static PayrollRunRecord Map(SqliteDataReader reader)
    {
        return new PayrollRunRecord(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetString(3),
            (int)reader.GetInt64(4),
            (int)reader.GetInt64(5),
            (int)reader.GetInt64(6),
            (int)reader.GetInt64(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetInt64(11),
            reader.GetInt64(12));
    }

    private const string SelectColumns =
        """
        id, personnel_id, start_date, end_date, overtime_minutes, deficit_minutes,
        leave_minutes, remaining_leave_minutes, status, created_at, updated_at,
        created_by, updated_by
        """;

    private sealed record PersonSnapshot(
        long Id,
        string? RemoteId,
        DateOnly? CooperationStart,
        DateOnly? CooperationEnd);
}
