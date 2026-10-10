using Atten.Core;
using Microsoft.Data.Sqlite;

namespace Atten.Data;

internal sealed class SqliteClockEventRepository : IClockEventRepository
{
    private readonly SqliteSession _session;

    public SqliteClockEventRepository(SqliteSession session)
    {
        _session = session;
    }

    public long Add(string remoteId, string name, DateOnly eventDate, string eventTime)
    {
        string personKey = RepositoryGuard.RequiredText(remoteId, "Remote ID را وارد کنید.");
        string personName = RepositoryGuard.RequiredText(name, "نام را وارد کنید.");
        string storedDate = RepositoryGuard.StorageDay(eventDate);
        string storedTime = RepositoryGuard.StorageClockTime(eventTime);
        string createdAt = _session.Timestamp();
        return _session.Write(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO clock_events (
                        remote_id, name, date, time, created_at, updated_at,
                        created_by, updated_by
                    )
                    VALUES (
                        $remote_id, $name, $date, $time, $created_at, $updated_at,
                        $created_by, $updated_by
                    )
                    """;
                command.Parameters.AddWithValue("$remote_id", personKey);
                command.Parameters.AddWithValue("$name", personName);
                command.Parameters.AddWithValue("$date", storedDate);
                command.Parameters.AddWithValue("$time", storedTime);
                command.Parameters.AddWithValue("$created_at", createdAt);
                command.Parameters.AddWithValue("$updated_at", createdAt);
                command.Parameters.AddWithValue("$created_by", _session.ActorId);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.ExecuteNonQuery();
                return SqliteReader.LastInsertId(connection);
            },
            RepositoryGuard.ClockSaveError);
    }

    public ClockEventImport Import(IReadOnlyList<AttlogRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var prepared = new List<(string RemoteId, string Name, string Date, string Time)>(rows.Count);
        foreach (AttlogRow row in rows)
        {
            prepared.Add(PreparedClockEvent(row));
        }

        string createdAt = _session.Timestamp();
        return _session.Write(
            connection =>
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                var existing = new HashSet<(string RemoteId, string Date, string Time)>();
                using (SqliteCommand select = connection.CreateCommand())
                {
                    select.Transaction = transaction;
                    select.CommandText = "SELECT remote_id, date, time FROM clock_events";
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        existing.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
                    }
                }

                var seen = new HashSet<(string RemoteId, string Date, string Time)>();
                int added = 0;
                int skipped = 0;
                foreach (var (remoteId, name, storedDate, storedTime) in prepared)
                {
                    var key = (remoteId, storedDate, storedTime);
                    if (existing.Contains(key) || seen.Contains(key))
                    {
                        skipped += 1;
                        continue;
                    }

                    using SqliteCommand insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText =
                        """
                        INSERT INTO clock_events (
                            remote_id, name, date, time, created_at, updated_at,
                            created_by, updated_by
                        )
                        VALUES (
                            $remote_id, $name, $date, $time, $created_at, $updated_at,
                            $created_by, $updated_by
                        )
                        """;
                    insert.Parameters.AddWithValue("$remote_id", remoteId);
                    insert.Parameters.AddWithValue("$name", name);
                    insert.Parameters.AddWithValue("$date", storedDate);
                    insert.Parameters.AddWithValue("$time", storedTime);
                    insert.Parameters.AddWithValue("$created_at", createdAt);
                    insert.Parameters.AddWithValue("$updated_at", createdAt);
                    insert.Parameters.AddWithValue("$created_by", _session.ActorId);
                    insert.Parameters.AddWithValue("$updated_by", _session.ActorId);
                    insert.ExecuteNonQuery();
                    seen.Add(key);
                    added += 1;
                }

                transaction.Commit();
                return new ClockEventImport(added, skipped);
            },
            RepositoryGuard.ClockSaveError);
    }

    public IReadOnlyList<ClockEventRecord> List()
    {
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, remote_id, name, date, time, created_at, updated_at,
                       created_by, updated_by
                FROM clock_events
                ORDER BY date, time, id
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            var rows = new List<ClockEventRecord>();
            while (reader.Read())
            {
                rows.Add(new ClockEventRecord(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    SqliteReader.Int64(reader, 7),
                    SqliteReader.Int64(reader, 8)));
            }

            return rows;
        });
    }

    public IReadOnlyList<DeviceDay> ListDaily(string remoteId, DateOnly startDate, DateOnly endDate)
    {
        string personKey = RepositoryGuard.RequiredText(remoteId, "Remote ID را وارد کنید.");
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT date, time
                FROM clock_events
                WHERE remote_id = $remote_id AND date >= $start AND date <= $end
                ORDER BY date, time, id
                """;
            command.Parameters.AddWithValue("$remote_id", personKey);
            command.Parameters.AddWithValue("$start", start);
            command.Parameters.AddWithValue("$end", end);
            using SqliteDataReader reader = command.ExecuteReader();
            var days = new List<DeviceDay>();
            string currentDate = "";
            var times = new List<string>();
            while (reader.Read())
            {
                string day = reader.GetString(0);
                if (day != currentDate)
                {
                    if (currentDate.Length > 0)
                    {
                        days.Add(new DeviceDay(currentDate, times));
                    }

                    currentDate = day;
                    times = [];
                }

                times.Add(reader.GetString(1));
            }

            if (currentDate.Length > 0)
            {
                days.Add(new DeviceDay(currentDate, times));
            }

            return days;
        });
    }

    private static (string RemoteId, string Name, string Date, string Time) PreparedClockEvent(AttlogRow row)
    {
        try
        {
            return (
                RepositoryGuard.RequiredText(row.RemoteId, "Remote ID را وارد کنید."),
                RepositoryGuard.RequiredText(row.Name, "نام را وارد کنید."),
                RepositoryGuard.StorageDay(RepositoryGuard.ParseStorageDay(row.EventDate)),
                RepositoryGuard.StorageClockTime(row.EventTime));
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"سطر {row.LineNumber}: {ex.Message}", ex);
        }
    }
}
