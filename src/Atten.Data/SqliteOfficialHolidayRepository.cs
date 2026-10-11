using Atten.Core;
using Microsoft.Data.Sqlite;

namespace Atten.Data;

internal sealed class SqliteOfficialHolidayRepository : IOfficialHolidayRepository
{
    private readonly SqliteSession _session;

    public SqliteOfficialHolidayRepository(SqliteSession session)
    {
        _session = session;
    }

    public long Add(DateOnly date, string title)
    {
        string stored = RepositoryGuard.StorageDay(date);
        string name = RepositoryGuard.RequiredText(title, RepositoryGuard.HolidayTitle);
        string createdAt = _session.Timestamp();
        return _session.Write(
            connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO official_holidays (
                        date, title, created_at, updated_at, created_by, updated_by
                    )
                    VALUES (
                        $date, $title, $created_at, $updated_at, $created_by, $updated_by
                    )
                    """;
                command.Parameters.AddWithValue("$date", stored);
                command.Parameters.AddWithValue("$title", name);
                command.Parameters.AddWithValue("$created_at", createdAt);
                command.Parameters.AddWithValue("$updated_at", createdAt);
                command.Parameters.AddWithValue("$created_by", _session.ActorId);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.ExecuteNonQuery();
                return SqliteReader.LastInsertId(connection);
            },
            RepositoryGuard.HolidaySaveError);
    }

    public void AddMany(IReadOnlyList<DateOnly> dates, string title)
    {
        IReadOnlyList<string> stored = StoredDays(dates);
        string name = RepositoryGuard.RequiredText(title, RepositoryGuard.HolidayTitle);
        string createdAt = _session.Timestamp();
        _session.Write(
            connection =>
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                foreach (string day in stored)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        INSERT INTO official_holidays (
                            date, title, created_at, updated_at, created_by, updated_by
                        )
                        VALUES (
                            $date, $title, $created_at, $updated_at, $created_by, $updated_by
                        )
                        """;
                    command.Parameters.AddWithValue("$date", day);
                    command.Parameters.AddWithValue("$title", name);
                    command.Parameters.AddWithValue("$created_at", createdAt);
                    command.Parameters.AddWithValue("$updated_at", createdAt);
                    command.Parameters.AddWithValue("$created_by", _session.ActorId);
                    command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            },
            RepositoryGuard.HolidaySaveError);
    }

    public OfficialHolidayImport Import(IReadOnlyList<OfficialHolidayDraft> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        var prepared = new List<(string Date, string Title)>(days.Count);
        foreach (OfficialHolidayDraft day in days)
        {
            ArgumentNullException.ThrowIfNull(day);
            prepared.Add((
                RepositoryGuard.StorageDay(day.Date),
                RepositoryGuard.RequiredText(day.Title, RepositoryGuard.HolidayTitle)));
        }

        if (prepared.Count == 0)
        {
            throw new ArgumentException(RepositoryGuard.HolidayDates);
        }

        string createdAt = _session.Timestamp();
        return _session.Write(
            connection =>
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                var existing = new HashSet<string>(StringComparer.Ordinal);
                using (SqliteCommand select = connection.CreateCommand())
                {
                    select.Transaction = transaction;
                    select.CommandText = "SELECT date FROM official_holidays";
                    using SqliteDataReader reader = select.ExecuteReader();
                    while (reader.Read())
                    {
                        existing.Add(reader.GetString(0));
                    }
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                int added = 0;
                int skipped = 0;
                foreach ((string stored, string title) in prepared)
                {
                    if (existing.Contains(stored) || !seen.Add(stored))
                    {
                        skipped += 1;
                        continue;
                    }

                    using SqliteCommand insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText =
                        """
                        INSERT INTO official_holidays (
                            date, title, created_at, updated_at, created_by, updated_by
                        )
                        VALUES (
                            $date, $title, $created_at, $updated_at, $created_by, $updated_by
                        )
                        """;
                    insert.Parameters.AddWithValue("$date", stored);
                    insert.Parameters.AddWithValue("$title", title);
                    insert.Parameters.AddWithValue("$created_at", createdAt);
                    insert.Parameters.AddWithValue("$updated_at", createdAt);
                    insert.Parameters.AddWithValue("$created_by", _session.ActorId);
                    insert.Parameters.AddWithValue("$updated_by", _session.ActorId);
                    insert.ExecuteNonQuery();
                    added += 1;
                }

                transaction.Commit();
                return new OfficialHolidayImport(added, skipped);
            },
            RepositoryGuard.HolidaySaveError);
    }

    public void Update(long holidayId, DateOnly date, string title)
    {
        long holidayKey = RepositoryGuard.HolidayId(holidayId);
        string stored = RepositoryGuard.StorageDay(date);
        string name = RepositoryGuard.RequiredText(title, RepositoryGuard.HolidayTitle);
        string updatedAt = _session.Timestamp();
        _session.Write(
            connection =>
            {
                using SqliteCommand found = connection.CreateCommand();
                found.CommandText = "SELECT 1 FROM official_holidays WHERE id = $id";
                found.Parameters.AddWithValue("$id", holidayKey);
                if (found.ExecuteScalar() is null)
                {
                    throw new ArgumentException(RepositoryGuard.MissingHoliday);
                }

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    UPDATE official_holidays
                    SET date = $date,
                        title = $title,
                        updated_at = $updated_at,
                        updated_by = $updated_by
                    WHERE id = $id
                    """;
                command.Parameters.AddWithValue("$date", stored);
                command.Parameters.AddWithValue("$title", name);
                command.Parameters.AddWithValue("$updated_at", updatedAt);
                command.Parameters.AddWithValue("$updated_by", _session.ActorId);
                command.Parameters.AddWithValue("$id", holidayKey);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.HolidaySaveError);
    }

    public void Delete(long holidayId)
    {
        long holidayKey = RepositoryGuard.HolidayId(holidayId);
        _session.Write(
            connection =>
            {
                using SqliteCommand found = connection.CreateCommand();
                found.CommandText = "SELECT 1 FROM official_holidays WHERE id = $id";
                found.Parameters.AddWithValue("$id", holidayKey);
                if (found.ExecuteScalar() is null)
                {
                    throw new ArgumentException(RepositoryGuard.MissingHoliday);
                }

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM official_holidays WHERE id = $id";
                command.Parameters.AddWithValue("$id", holidayKey);
                command.ExecuteNonQuery();
            },
            RepositoryGuard.HolidayDeleteError);
    }

    public void DeleteMany(IReadOnlyList<long> holidayIds)
    {
        ArgumentNullException.ThrowIfNull(holidayIds);
        if (holidayIds.Count == 0)
        {
            throw new ArgumentException(RepositoryGuard.ChooseHoliday);
        }

        long[] keys = holidayIds.Select(RepositoryGuard.HolidayId).Distinct().ToArray();
        _session.Write(
            connection =>
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                foreach (long holidayKey in keys)
                {
                    using SqliteCommand found = connection.CreateCommand();
                    found.Transaction = transaction;
                    found.CommandText = "SELECT 1 FROM official_holidays WHERE id = $id";
                    found.Parameters.AddWithValue("$id", holidayKey);
                    if (found.ExecuteScalar() is null)
                    {
                        throw new ArgumentException(RepositoryGuard.MissingHoliday);
                    }

                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM official_holidays WHERE id = $id";
                    command.Parameters.AddWithValue("$id", holidayKey);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            },
            RepositoryGuard.HolidayDeleteError);
    }

    public IReadOnlyList<OfficialHolidayRecord> List()
    {
        return Read(start: null, end: null);
    }

    public IReadOnlyList<OfficialHolidayRecord> List(DateOnly startDate, DateOnly endDate)
    {
        var (start, end) = RepositoryGuard.DateSpan(startDate, endDate);
        return Read(start, end);
    }

    private IReadOnlyList<OfficialHolidayRecord> Read(string? start, string? end)
    {
        return _session.Read(connection =>
        {
            using SqliteCommand command = connection.CreateCommand();
            if (start is null || end is null)
            {
                command.CommandText =
                    """
                    SELECT id, date, title, created_at, updated_at, created_by, updated_by
                    FROM official_holidays
                    ORDER BY date, id
                    """;
            }
            else
            {
                command.CommandText =
                    """
                    SELECT id, date, title, created_at, updated_at, created_by, updated_by
                    FROM official_holidays
                    WHERE date >= $start AND date <= $end
                    ORDER BY date, id
                    """;
                command.Parameters.AddWithValue("$start", start);
                command.Parameters.AddWithValue("$end", end);
            }

            using SqliteDataReader reader = command.ExecuteReader();
            var rows = new List<OfficialHolidayRecord>();
            while (reader.Read())
            {
                rows.Add(new OfficialHolidayRecord(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetInt64(5),
                    reader.GetInt64(6)));
            }

            return rows;
        });
    }

    private static IReadOnlyList<string> StoredDays(IReadOnlyList<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        if (dates.Count == 0)
        {
            throw new ArgumentException(RepositoryGuard.HolidayDates);
        }

        var stored = new List<string>(dates.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (DateOnly date in dates)
        {
            string day = RepositoryGuard.StorageDay(date);
            if (!seen.Add(day))
            {
                throw new ArgumentException(RepositoryGuard.HolidayDuplicate);
            }

            stored.Add(day);
        }

        return stored;
    }
}
