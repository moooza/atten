using Atten.Core;

namespace Atten.Data;

/// <summary>
/// Personnel, leave, clock, calculation, and payroll access behind interfaces.
/// The portable file path and the SQLite connection stay inside Data.
/// </summary>
public interface IAttenRepository
{
    IPersonnelRepository Personnel { get; }

    ILeaveRepository Leaves { get; }

    IClockEventRepository ClockEvents { get; }

    ICalculationRepository Calculation { get; }

    IPayrollRepository Payroll { get; }

    IUserRepository Users { get; }

    IBackupService Backup { get; }
}

public interface IBackupService
{
    string SuggestedFolder { get; }

    string Export(string folder, DateTime? moment = null);

    void Validate(string source);

    void Restore(string source);

    BackupHealth Check(string path, Action<int, int, string>? onProgress = null);

    string Describe(BackupHealth result);
}

public interface IPersonnelRepository
{
    long Add(
        string firstName,
        string lastName,
        double dailyHours,
        string? remoteId = null,
        string? mobile = null,
        DateOnly? cooperationStart = null,
        DateOnly? cooperationEnd = null);

    void Update(
        long personId,
        string firstName,
        string lastName,
        double dailyHours,
        string? remoteId = null,
        string? mobile = null,
        DateOnly? cooperationStart = null,
        DateOnly? cooperationEnd = null);

    IReadOnlyList<PersonnelRecord> List();
}

public interface ILeaveRepository
{
    long Add(long personnelId, DateOnly startDate, DateOnly endDate, int minutes);

    void Update(long leaveId, long personnelId, DateOnly startDate, DateOnly endDate, int minutes);

    IReadOnlyList<LeaveRecord> List(long personnelId, DateOnly? startDate = null, DateOnly? endDate = null);

    int SumMinutes(long personnelId, DateOnly startDate, DateOnly endDate);
}

public interface IClockEventRepository
{
    long Add(string remoteId, string name, DateOnly eventDate, string eventTime);

    ClockEventImport Import(IReadOnlyList<AttlogRow> rows);

    IReadOnlyList<ClockEventRecord> List();

    IReadOnlyList<DeviceDay> ListDaily(string remoteId, DateOnly startDate, DateOnly endDate);
}

public interface ICalculationRepository
{
    IReadOnlyDictionary<string, IReadOnlyDictionary<int, string?>> ListPunches(
        string remoteId,
        DateOnly startDate,
        DateOnly endDate);

    void SavePunch(string remoteId, DateOnly eventDate, int slot, string? eventTime);

    void ClearPunches(string remoteId, DateOnly eventDate);

    IReadOnlyList<WorkBalance> ListBalances(string remoteId, DateOnly startDate, DateOnly endDate);

    void SaveBalances(string remoteId, IEnumerable<(DateOnly Date, int Minutes, bool Holiday)> balances);

    void SaveHoliday(string remoteId, DateOnly eventDate, bool holiday);
}

public interface IPayrollRepository
{
    PayrollRunRecord Draft(long personnelId, DateOnly startDate, DateOnly endDate);

    PayrollRunRecord Recalculate(long payrollId);

    void Confirm(long payrollId);

    void Reopen(long payrollId);

    PayrollRunRecord? Get(long payrollId);

    IReadOnlyList<PayrollRunRecord> List(long? personnelId = null);
}

public interface IUserRepository
{
    UserRecord? Get(long userId);

    IReadOnlyList<UserRecord> List();
}

/// <summary>Open the store without handing a SQLite type to App.</summary>
public static class AttenStore
{
    /// <summary>Repair a damaged file if needed, migrate, then open as the default user.</summary>
    public static IAttenRepository Start()
    {
        return Start(out _);
    }

    public static IAttenRepository Start(out string? notice)
    {
        return Start(AppPaths.DbFile(), DefaultUser.Id, out notice);
    }

    public static IAttenRepository Start(string dbFile)
    {
        return Start(dbFile, DefaultUser.Id);
    }

    public static IAttenRepository Start(string dbFile, long actorId)
    {
        return Start(dbFile, actorId, out _);
    }

    public static IAttenRepository Start(string dbFile, long actorId, out string? notice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        notice = DatabaseBackup.Prepare(dbFile);
        return Open(dbFile, actorId);
    }

    public static IAttenRepository Open()
    {
        return Open(AppPaths.DbFile(), DefaultUser.Id);
    }

    public static IAttenRepository Open(string dbFile)
    {
        return Open(dbFile, DefaultUser.Id);
    }

    public static IAttenRepository Open(string dbFile, long actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        if (actorId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actorId));
        }

        return new SqliteAttenRepository(dbFile, actorId);
    }
}

public sealed record PersonnelRecord(
    long Id,
    string? RemoteId,
    string FirstName,
    string LastName,
    double DailyHours,
    string? Mobile,
    string? CooperationStart,
    string? CooperationEnd,
    string? CreatedAt,
    string? UpdatedAt,
    long? CreatedBy,
    long? UpdatedBy);

public sealed record LeaveRecord(
    long Id,
    long PersonnelId,
    string StartDate,
    string EndDate,
    int Minutes,
    string CreatedAt,
    string UpdatedAt,
    long? CreatedBy,
    long? UpdatedBy);

public sealed record ClockEventRecord(
    long Id,
    string RemoteId,
    string Name,
    string Date,
    string Time,
    string CreatedAt,
    string UpdatedAt,
    long? CreatedBy,
    long? UpdatedBy);

public sealed record ClockEventImport(int Added, int Skipped);

public sealed record WorkBalance(
    string RemoteId,
    string Date,
    int BalanceMinutes,
    bool Holiday);

public sealed record PayrollRunRecord(
    long Id,
    long PersonnelId,
    string StartDate,
    string EndDate,
    int OvertimeMinutes,
    int DeficitMinutes,
    int LeaveMinutes,
    int RemainingLeaveMinutes,
    string Status,
    string CreatedAt,
    string UpdatedAt,
    long CreatedBy,
    long UpdatedBy);

public sealed record UserRecord(long Id, string DisplayName, string CreatedAt);
