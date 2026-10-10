using Atten.Core;
using Atten.Data;
using CorePayroll = Atten.Core.Payroll;

namespace Atten.App.Payroll;

public sealed class PayrollRow
{
    public PayrollRow(PayrollRunRecord record, string actorName)
    {
        Record = record;
        Period = CorePayroll.FormatPeriod(record.StartDate);
        Overtime = Sheet.FormatBalance(record.OvertimeMinutes);
        Deficit = Sheet.FormatBalance(-record.DeficitMinutes);
        LeaveAmount = Leave.FormatLeaveAmount(record.LeaveMinutes);
        Remaining = Leave.FormatSignedLeaveAmount(record.RemainingLeaveMinutes);
        Status = CorePayroll.FormatStatus(record.Status);
        Actor = actorName;
        UpdatedAt = string.IsNullOrWhiteSpace(record.UpdatedAt)
            ? string.Empty
            : Dates.FormatShamsiDateTime(record.UpdatedAt, seconds: true);
    }

    public PayrollRunRecord Record { get; }

    public string Period { get; }

    public string Overtime { get; }

    public string Deficit { get; }

    public string LeaveAmount { get; }

    public string Remaining { get; }

    public string Status { get; }

    public string Actor { get; }

    public string UpdatedAt { get; }
}
