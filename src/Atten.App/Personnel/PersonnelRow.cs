using System.Globalization;
using Atten.Core;
using Atten.Data;

namespace Atten.App.Personnel;

public sealed class PersonnelRow
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public PersonnelRow(PersonnelRecord record)
    {
        Record = record;
        IdText = record.Id.ToString(Invariant);
        RemoteId = record.RemoteId ?? string.Empty;
        FirstName = record.FirstName;
        LastName = record.LastName;
        DailyHours = PersonnelHours.Format(record.DailyHours);
        Mobile = record.Mobile ?? string.Empty;
        CreatedAt = Stamp(record.CreatedAt);
        UpdatedAt = Stamp(record.UpdatedAt);
    }

    public PersonnelRecord Record { get; }

    public string IdText { get; }

    public string RemoteId { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public string DailyHours { get; }

    public string Mobile { get; }

    public string CreatedAt { get; }

    public string UpdatedAt { get; }

    private static string Stamp(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : Dates.FormatShamsiDateTime(value);
    }
}
