using Atten.Core;

namespace Atten.Data;

/// <summary>
/// Personnel, leave, clock, calculation, payroll, and official holiday access behind interfaces.
/// The portable file path and the SQLite connection stay inside Data.
/// </summary>
public interface IAttenRepository
{
    IPersonnelRepository Personnel { get; }

    ILeaveRepository Leaves { get; }

    IClockEventRepository ClockEvents { get; }

    ICalculationRepository Calculation { get; }

    IPayrollRepository Payroll { get; }

    IOfficialHolidayRepository OfficialHolidays { get; }

    IUserRepository Users { get; }

    IBackupService Backup { get; }
}

public interface IBackupService
{
    string SuggestedFolder { get; }

    string Export(string folder, DateTime? moment = null);

    void Validate(string source);

    void Restore(string source);

    IReadOnlyList<BackupSnapshot> ListSnapshots();

    void RestoreSnapshot(string id);

    int SnapshotKeep { get; }

    void ApplySnapshotKeep(int keep);

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
    long Add(long personnelId, DateOnly startDate, DateOnly endDate, int minutes, string? description = null);

    void Update(long leaveId, long personnelId, DateOnly startDate, DateOnly endDate, int minutes, string? description = null);

    void Delete(long leaveId);

    IReadOnlyList<LeaveRecord> List(long personnelId, DateOnly? startDate = null, DateOnly? endDate = null);

    int SumMinutes(long personnelId, DateOnly startDate, DateOnly endDate);

    IReadOnlyDictionary<int, int> CarryMinutesByYear(long personnelId);

    void ApplyLeftover(long personnelId, int year, int transferMinutes, int settleMinutes);
}

public interface IClockEventRepository
{
    long Add(string remoteId, string name, DateOnly eventDate, string eventTime);

    ClockEventImport Import(IReadOnlyList<AttlogRow> rows);

    IReadOnlyList<ClockEventRecord> List(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        int skip = 0,
        int? take = null);

    int Count(DateOnly? startDate = null, DateOnly? endDate = null);

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

public interface IOfficialHolidayRepository
{
    long Add(DateOnly date, string title);

    void AddMany(IReadOnlyList<DateOnly> dates, string title);

    OfficialHolidayImport Import(IReadOnlyList<OfficialHolidayDraft> days);

    void Update(long holidayId, DateOnly date, string title);

    void Delete(long holidayId);

    void DeleteMany(IReadOnlyList<long> holidayIds);

    IReadOnlyList<OfficialHolidayRecord> List();

    IReadOnlyList<OfficialHolidayRecord> List(DateOnly startDate, DateOnly endDate);
}

public interface IUserRepository
{
    UserRecord? Get(long userId);

    IReadOnlyList<UserRecord> List();
}

/// <summary>Open the store without handing a SQLite type to App.</summary>
public static class AttenStore
{
    /// <summary>Migrate a healthy or missing file, then open as the default user. Does not restore a damaged file.</summary>
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
        notice = null;
        EnsureReady(dbFile);
        return Open(dbFile, actorId);
    }

    public static int SnapshotKeep
    {
        get => DatabaseBackup.KeepSnapshots;
        set => DatabaseBackup.KeepSnapshots = value;
    }

    public static DatabaseInspection Inspect()
    {
        return Inspect(AppPaths.DbFile());
    }

    public static DatabaseInspection Inspect(string dbFile)
    {
        return DatabaseBackup.Inspect(dbFile);
    }

    public static void EnsureReady()
    {
        EnsureReady(AppPaths.DbFile());
    }

    public static void EnsureReady(string dbFile)
    {
        DatabaseBackup.EnsureReady(dbFile);
    }

    /// <summary>Backup helpers without opening the store. Safe when the live file is damaged.</summary>
    public static IBackupService Backup()
    {
        return Backup(AppPaths.DbFile());
    }

    public static IBackupService Backup(string dbFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFile);
        return new SqliteBackupService(dbFile);
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
    string Kind,
    string? Description,
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

public sealed record ClockEventImport(int Added, int Skipped, IReadOnlyList<AttlogIssue> Errors)
{
    public int Failed => Errors.Count;
}

public sealed record WorkBalance(
    string RemoteId,
    string Date,
    int BalanceMinutes,
    bool Holiday,
    bool? HolidayOverride = null);

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

public sealed record OfficialHolidayRecord(
    long Id,
    string Date,
    string Title,
    string CreatedAt,
    string UpdatedAt,
    long CreatedBy,
    long UpdatedBy);

public sealed record OfficialHolidayDraft(DateOnly Date, string Title);

public sealed record OfficialHolidayImport(int Added, int Skipped);
