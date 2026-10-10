using System.Globalization;
using Atten.Core;
using Atten.Data;

namespace Atten.App.ClockEvents;

public sealed class ClockEventRow
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public ClockEventRow(ClockEventRecord record)
    {
        Record = record;
        IdText = record.Id.ToString(Invariant);
        RemoteId = record.RemoteId;
        Name = record.Name;
        Weekday = Dates.FormatShamsiWeekday(record.Date);
        Date = Dates.FormatShamsiDate(record.Date);
        Time = record.Time;
        CreatedAt = Stamp(record.CreatedAt);
        UpdatedAt = Stamp(record.UpdatedAt);
    }

    public ClockEventRecord Record { get; }

    public string IdText { get; }

    public string RemoteId { get; }

    public string Name { get; }

    public string Weekday { get; }

    public string Date { get; }

    public string Time { get; }

    public string CreatedAt { get; }

    public string UpdatedAt { get; }

    private static string Stamp(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : Dates.FormatShamsiDateTime(value, seconds: true);
    }
}
