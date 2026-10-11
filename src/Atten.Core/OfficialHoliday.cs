namespace Atten.Core;

/// <summary>
/// Country holidays come from a catalog the caller already loaded.
/// Friday stays a week rule. A person-day tick wins over both.
/// </summary>
public static class OfficialHoliday
{
    public static IReadOnlySet<string> Catalog(IEnumerable<string> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (string date in dates)
        {
            ArgumentNullException.ThrowIfNull(date);
            set.Add(date);
        }

        return set;
    }

    public static IReadOnlySet<string> Catalog(IEnumerable<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        return Catalog(dates.Select(Dates.StorageDate));
    }

    public static bool Contains(IReadOnlySet<string> catalog, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Contains(Dates.StorageDate(day));
    }

    public static bool Contains(IReadOnlySet<string> catalog, string date)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(date);
        return catalog.Contains(date);
    }

    /// <summary>
    /// Person override, then the official catalog, then Friday, otherwise a working day.
    /// </summary>
    public static bool IsHoliday(
        DateOnly day,
        IReadOnlySet<string>? official = null,
        bool? personOverride = null)
    {
        if (personOverride is not null)
        {
            return personOverride.Value;
        }

        if (official is not null && Contains(official, day))
        {
            return true;
        }

        return day.DayOfWeek == DayOfWeek.Friday;
    }
}
