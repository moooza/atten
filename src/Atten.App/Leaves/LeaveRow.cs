using System.Globalization;
using Atten.Core;
using Atten.Data;

namespace Atten.App.Leaves;

public sealed class LeaveRow
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public LeaveRow(LeaveRecord record)
    {
        Record = record;
        IdText = record.Id.ToString(Invariant);
        StartDate = Dates.FormatShamsiDate(record.StartDate);
        EndDate = Dates.FormatShamsiDate(record.EndDate);
        Amount = Leave.FormatLeaveAmount(record.Minutes);
        CreatedAt = Stamp(record.CreatedAt);
    }

    public LeaveRecord Record { get; }

    public string IdText { get; }

    public string StartDate { get; }

    public string EndDate { get; }

    public string Amount { get; }

    public string CreatedAt { get; }

    private static string Stamp(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : Dates.FormatShamsiDateTime(value, seconds: true);
    }
}
