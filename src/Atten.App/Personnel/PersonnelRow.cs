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
    }

    public PersonnelRecord Record { get; }

    public string IdText { get; }

    public string RemoteId { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public string DailyHours { get; }
}
