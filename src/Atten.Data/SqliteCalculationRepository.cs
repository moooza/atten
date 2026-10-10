using Microsoft.Data.Sqlite;

namespace Atten.Data;

internal sealed class SqliteCalculationRepository : ICalculationRepository
{
    private readonly SqliteSession _session;

    public SqliteCalculationRepository(SqliteSession session)
    {
        _session = session;
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, string?>> ListPunches(
        string remoteId,
        DateOnly startDate,
        DateOnly endDate)
    {
        string personKey = RepositoryGuard.RequiredText(remoteId, "Remote ID را وارد کنید.");
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT date, slot, time
                FROM calculation_punches
                WHERE remote_id = $remote_id AND date >= $start AND date <= $end
                ORDER BY date, slot
                """;
            command.Parameters.AddWithValue("$remote_id", personKey);
            command.Parameters.AddWithValue("$start", start);
            command.Parameters.AddWithValue("$end", end);
            using SqliteDataReader reader = command.ExecuteReader();
            var grouped = new Dictionary<string, Dictionary<int, string?>>();
            while (reader.Read())
            {
                string day = reader.GetString(0);
                if (!grouped.TryGetValue(day, out Dictionary<int, string?>? slots))
                {
                    slots = [];
                    grouped[day] = slots;
                }

                slots[(int)reader.GetInt64(1)] = SqliteReader.Text(reader, 2);
            }

            return grouped.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<int, string?>)pair.Value);
        });
    }

    public void SavePunch(string remoteId, DateOnly eventDate, int slot, string? eventTime)
    {
        string personKey = RepositoryGuard.RequiredText(remoteId, "Remote ID را وارد کنید.");
        string storedDate = RepositoryGuard.StorageDay(eventDate);
        if (slot < 0)
        {
            throw new ArgumentException(RepositoryGuard.InvalidTime);
        }

        string? storedTime = eventTime is null || eventTime.Trim().Length == 0
            ? null
            : RepositoryGuard.StorageClockTime(eventTime);
        string stamp = _session.Timestamp();
        _session.Write(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO calculation_punches (
                        remote_id, date, slot, time, created_at, updated_at,
                        created_by, updated_by
                    )
                    VALUES (
                        $remote_id, $date, $slot, $time, $created_at, $updated_at,
                        $created_by, $updated_by
                    )
                    ON CONFLICT (remote_id, date, slot) DO UPDATE SET
                        time = excluded.time,
                        updated_at = excluded.updated_at,
                        updated_by = excluded.updated_by
                    """;
                command.Parameters.AddWithValue("$remote_id", personKey);
                command.Parameters.AddWithValue("$date", storedDate);
                command.Parameters.AddWithValue("$slot", slot);
                command.Parameters.AddWithValue("$time", RepositoryGuard.Db(storedTime));
                command.Parameters.AddWithValue("$created_at", stamp);
                command.Parameters.AddWithValue("$updated_at", stamp);
                command.Parameters.AddWithValue("$created_by", _session.ActorId);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.ClockSaveError);
    }

    public void ClearPunches(string remoteId, DateOnly eventDate)
    {
        string personKey = RepositoryGuard.RequiredText(remoteId, "Remote ID را وارد کنید.");
        string storedDate = RepositoryGuard.StorageDay(eventDate);
        _session.Write(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    DELETE FROM calculation_punches
                    WHERE remote_id = $remote_id AND date = $date
                    """;
                command.Parameters.AddWithValue("$remote_id", personKey);
                command.Parameters.AddWithValue("$date", storedDate);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.ClockSaveError);
    }

    public IReadOnlyList<WorkBalance> ListBalances(string remoteId, DateOnly startDate, DateOnly endDate)
    {
        string personKey = RepositoryGuard.RequiredText(remoteId, "Remote ID را وارد کنید.");
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT remote_id, date, balance_minutes, holiday
                FROM calculation_days
                WHERE remote_id = $remote_id AND date >= $start AND date <= $end
                ORDER BY date
                """;
            command.Parameters.AddWithValue("$remote_id", personKey);
            command.Parameters.AddWithValue("$start", start);
            command.Parameters.AddWithValue("$end", end);
            using SqliteDataReader reader = command.ExecuteReader();
            var rows = new List<WorkBalance>();
            while (reader.Read())
            {
                rows.Add(new WorkBalance(
                    reader.GetString(0),
                    reader.GetString(1),
                    (int)reader.GetInt64(2),
                    !reader.IsDBNull(3) && reader.GetInt64(3) != 0));
            }

            return rows;
        });
    }

    public void SaveBalances(string remoteId, IEnumerable<(DateOnly Date, int Minutes, bool Holiday)> balances)
    {
        ArgumentNullException.ThrowIfNull(balances);
        string personKey = RepositoryGuard.RequiredText(remoteId, "Remote ID را وارد کنید.");
        string stamp = _session.Timestamp();
        var prepared = balances
            .Select(item => (RepositoryGuard.StorageDay(item.Date), item.Minutes, item.Holiday ? 1 : 0))
            .ToList();
        _session.Write(
            connection =>
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                foreach (var (day, minutes, holiday) in prepared)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        INSERT INTO calculation_days (
                            remote_id, date, balance_minutes, holiday, created_at, updated_at,
                            created_by, updated_by
                        )
                        VALUES (
                            $remote_id, $date, $balance_minutes, $holiday, $created_at, $updated_at,
                            $created_by, $updated_by
                        )
                        ON CONFLICT (remote_id, date) DO UPDATE SET
                            balance_minutes = excluded.balance_minutes,
                            holiday = excluded.holiday,
                            updated_at = excluded.updated_at,
                            updated_by = excluded.updated_by
                        """;
                    command.Parameters.AddWithValue("$remote_id", personKey);
                    command.Parameters.AddWithValue("$date", day);
                    command.Parameters.AddWithValue("$balance_minutes", minutes);
                    command.Parameters.AddWithValue("$holiday", holiday);
                    command.Parameters.AddWithValue("$created_at", stamp);
                    command.Parameters.AddWithValue("$updated_at", stamp);
                    command.Parameters.AddWithValue("$created_by", _session.ActorId);
                    command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            },
            RepositoryGuard.ClockSaveError);
    }

    public void SaveHoliday(string remoteId, DateOnly eventDate, bool holiday)
    {
        string personKey = RepositoryGuard.RequiredText(remoteId, "Remote ID را وارد کنید.");
        string storedDate = RepositoryGuard.StorageDay(eventDate);
        string stamp = _session.Timestamp();
        int flag = holiday ? 1 : 0;
        _session.Write(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO calculation_days (
                        remote_id, date, balance_minutes, holiday, created_at, updated_at,
                        created_by, updated_by
                    )
                    VALUES (
                        $remote_id, $date, 0, $holiday, $created_at, $updated_at,
                        $created_by, $updated_by
                    )
                    ON CONFLICT (remote_id, date) DO UPDATE SET
                        holiday = excluded.holiday,
                        updated_at = excluded.updated_at,
                        updated_by = excluded.updated_by
                    """;
                command.Parameters.AddWithValue("$remote_id", personKey);
                command.Parameters.AddWithValue("$date", storedDate);
                command.Parameters.AddWithValue("$holiday", flag);
                command.Parameters.AddWithValue("$created_at", stamp);
                command.Parameters.AddWithValue("$updated_at", stamp);
                command.Parameters.AddWithValue("$created_by", _session.ActorId);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.ClockSaveError);
    }
}
