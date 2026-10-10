namespace Atten.Data;

internal sealed class SqliteAttenRepository : IAttenRepository
{
    public SqliteAttenRepository(string dbFile, long actorId)
    {
        var session = new SqliteSession(dbFile, actorId);
        Personnel = new SqlitePersonnelRepository(session);
        Leaves = new SqliteLeaveRepository(session);
        ClockEvents = new SqliteClockEventRepository(session);
        Calculation = new SqliteCalculationRepository(session);
        Payroll = new SqlitePayrollRepository(session);
        Users = new SqliteUserRepository(session);
        Backup = new SqliteBackupService(dbFile);
    }

    public IPersonnelRepository Personnel { get; }

    public ILeaveRepository Leaves { get; }

    public IClockEventRepository ClockEvents { get; }

    public ICalculationRepository Calculation { get; }

    public IPayrollRepository Payroll { get; }

    public IUserRepository Users { get; }

    public IBackupService Backup { get; }
}
