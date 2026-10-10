using System.Globalization;
using Atten.Core;
using Microsoft.Data.Sqlite;

namespace Atten.Data;

internal sealed class SqliteSession
{
    public SqliteSession(string dbFile, long actorId)
    {
        DbFile = dbFile;
        ActorId = actorId;
    }

    public string DbFile { get; }

    public long ActorId { get; }

    public T Read<T>(Func<SqliteConnection, T> func)
    {
        return SqliteDatabase.Use(DbFile, func);
    }

    public T Write<T>(Func<SqliteConnection, T> func, Func<SqliteException, Exception> onError)
    {
        try
        {
            T result = SqliteDatabase.Use(DbFile, func);
            DatabaseBackup.TrySnapshot(DbFile);
            return result;
        }
        catch (SqliteException ex)
        {
            throw onError(ex);
        }
    }

    public void Write(Action<SqliteConnection> action, Func<SqliteException, Exception> onError)
    {
        Write(
            connection =>
            {
                action(connection);
                return 0;
            },
            onError);
    }

    public string Timestamp()
    {
        return StorageClock.Now();
    }
}

internal static class StorageClock
{
    internal static Func<string>? Override { get; set; }

    internal static string Now()
    {
        return Override?.Invoke() ?? Dates.StorageDateTime(DateTime.Now);
    }

    internal static IDisposable Freeze(string timestamp)
    {
        Override = () => timestamp;
        return new FreezeScope();
    }

    internal static void Reset()
    {
        Override = null;
    }

    private sealed class FreezeScope : IDisposable
    {
        public void Dispose()
        {
            Override = null;
        }
    }
}

internal static class RepositoryGuard
{
    public const string InvalidRange = "تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.";
    public const string InvalidDate = "تاریخ معتبر نیست.";
    public const string InvalidTime = "ساعت معتبر نیست.";
    public const string MissingPerson = "این پرسنل دیگر وجود ندارد.";
    public const string MissingLeave = "این مرخصی دیگر وجود ندارد.";
    public const string ChoosePerson = "یک پرسنل را انتخاب کنید.";
    public const string ChooseLeave = "یک مرخصی را انتخاب کنید.";
    public const string LeaveAmount = "میزان مرخصی را وارد کنید.";
    public const string PersonnelSave = "ذخیره پرسنل ممکن نشد.";
    public const string LeaveSave = "ثبت مرخصی ممکن نشد.";
    public const string ClockSave = "ثبت ورود و خروج ممکن نشد.";
    public const string MissingPayroll = "این حقوق دیگر وجود ندارد.";
    public const string ChoosePayroll = "یک حقوق را انتخاب کنید.";
    public const string PayrollSave = "ثبت حقوق ممکن نشد.";
    public const string PayrollConfirmed = "برای این پرسنل در این بازه قبلاً حقوق تأیید شده است.";

    public static long PersonId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentException(ChoosePerson);
        }

        return value;
    }

    public static long LeaveId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentException(ChooseLeave);
        }

        return value;
    }

    public static int LeaveMinutes(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentException(LeaveAmount);
        }

        return value;
    }

    public static string RequiredText(string? value, string message)
    {
        string text = value is null ? "" : Persian.NormalizeText(value.Trim());
        if (text.Length == 0)
        {
            throw new ArgumentException(message);
        }

        return text;
    }

    public static string? BlankToNone(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string text = Persian.NormalizeText(value.Trim());
        return text.Length == 0 ? null : text;
    }

    public static string StorageDay(DateOnly value)
    {
        return Dates.StorageDate(value);
    }

    public static DateOnly ParseStorageDay(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string text = value.Trim();
        string iso = text.Length >= 10 ? text[..10] : text;
        if (!DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day))
        {
            throw new ArgumentException(InvalidDate);
        }

        return day;
    }

    public static (string Start, string End) DateSpan(DateOnly startDate, DateOnly endDate)
    {
        string start = StorageDay(startDate);
        string end = StorageDay(endDate);
        if (string.CompareOrdinal(start, end) > 0)
        {
            throw new ArgumentException(InvalidRange);
        }

        return (start, end);
    }

    public static string StorageClockTime(string value)
    {
        try
        {
            return Sheet.ParseClock(value) ?? throw new ArgumentException(InvalidTime);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(InvalidTime, ex);
        }
    }

    public static (string? RemoteId, string FirstName, string LastName, double Hours, string? Mobile, string? Started, string? Ended)
        PersonnelFields(
            string firstName,
            string lastName,
            double dailyHours,
            string? remoteId,
            string? mobile,
            DateOnly? cooperationStart,
            DateOnly? cooperationEnd)
    {
        string first = Persian.NormalizeText(firstName.Trim());
        string last = Persian.NormalizeText(lastName.Trim());
        if (first.Length == 0)
        {
            throw new ArgumentException("نام را وارد کنید.");
        }

        if (last.Length == 0)
        {
            throw new ArgumentException("نام خانوادگی را وارد کنید.");
        }

        if (!double.IsFinite(dailyHours) || dailyHours <= 0)
        {
            throw new ArgumentException("ساعت کاری باید یک عدد بزرگ‌تر از صفر باشد.");
        }

        var (started, ended) = CooperationDates(cooperationStart, cooperationEnd);
        return (BlankToNone(remoteId), first, last, dailyHours, BlankToNone(mobile), started, ended);
    }

    public static Exception PersonnelSaveError(SqliteException ex)
    {
        string message = ex.Message.ToLowerInvariant();
        if (message.Contains("unique", StringComparison.Ordinal) &&
            message.Contains("remote_id", StringComparison.Ordinal))
        {
            return new ArgumentException("این کد پرسنلی قبلاً ثبت شده است.", ex);
        }

        return new ArgumentException(PersonnelSave, ex);
    }

    public static Exception LeaveSaveError(SqliteException ex)
    {
        return new ArgumentException(LeaveSave, ex);
    }

    public static Exception ClockSaveError(SqliteException ex)
    {
        return new ArgumentException(ClockSave, ex);
    }

    public static long PayrollId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentException(ChoosePayroll);
        }

        return value;
    }

    public static Exception PayrollSaveError(SqliteException ex)
    {
        string message = ex.Message.ToLowerInvariant();
        if (message.Contains("unique", StringComparison.Ordinal))
        {
            return new ArgumentException(PayrollConfirmed, ex);
        }

        return new ArgumentException(PayrollSave, ex);
    }

    public static void RequirePersonnel(SqliteConnection connection, SqliteTransaction? transaction, long personId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM personnel WHERE id = $id";
        command.Parameters.AddWithValue("$id", personId);
        if (command.ExecuteScalar() is null)
        {
            throw new ArgumentException(MissingPerson);
        }
    }

    public static object Db(object? value)
    {
        return value ?? DBNull.Value;
    }

    private static (string? Started, string? Ended) CooperationDates(DateOnly? start, DateOnly? end)
    {
        string? started = start is null ? null : StorageDay(start.Value);
        string? ended = end is null ? null : StorageDay(end.Value);
        if (ended is not null && started is null)
        {
            throw new ArgumentException("تاریخ شروع همکاری را وارد کنید.");
        }

        if (started is not null && ended is not null && string.CompareOrdinal(ended, started) < 0)
        {
            throw new ArgumentException("تاریخ پایان همکاری باید بعد از تاریخ شروع یا برابر با آن باشد.");
        }

        return (started, ended);
    }
}

internal static class SqliteReader
{
    public static string? Text(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static long? Int64(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    public static long LastInsertId(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
