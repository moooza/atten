namespace Atten.Core;

/// <summary>
/// One official holiday day. The date is Gregorian; the title is for the screen.
/// </summary>
public sealed record OfficialHolidayDay(DateOnly Date, string Title);

/// <summary>
/// A year pack is solar days from the Shamsi calendar plus a curated lunar table.
/// Years without a lunar table have no pack. The caller stores the rows; a later
/// edit can move a shifting day such as Eid Fitr.
/// </summary>
public static class OfficialHolidayYearPack
{
    public const string Nowruz = "نوروز";
    public const string RepublicDay = "روز جمهوری اسلامی";
    public const string NatureDay = "روز طبیعت";
    public const string Khomeini = "رحلت امام خمینی";
    public const string Khordad15 = "قیام ۱۵ خرداد";
    public const string Revolution = "پیروزی انقلاب اسلامی";
    public const string OilDay = "ملی شدن صنعت نفت";

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<(string Shamsi, string Title)>> LunarDays =
        new Dictionary<int, IReadOnlyList<(string Shamsi, string Title)>>
        {
            [1404] =
            [
                ("1404/01/01", "شهادت امام علی"),
                ("1404/01/11", "عید فطر"),
                ("1404/01/12", "عید فطر"),
                ("1404/02/04", "شهادت امام جعفر صادق"),
                ("1404/03/16", "عید قربان"),
                ("1404/03/24", "عید غدیر"),
                ("1404/04/14", "تاسوعا"),
                ("1404/04/15", "عاشورا"),
                ("1404/05/23", "اربعین"),
                ("1404/06/01", "رحلت رسول اکرم و شهادت امام حسن"),
                ("1404/06/02", "شهادت امام رضا"),
                ("1404/06/10", "شهادت امام حسن عسکری"),
                ("1404/06/19", "میلاد رسول اکرم و امام جعفر صادق"),
                ("1404/09/03", "شهادت حضرت فاطمه"),
                ("1404/10/12", "ولادت امام علی"),
                ("1404/10/27", "مبعث حضرت رسول اکرم صلی الله علیه و آل و سلم"),
                ("1404/11/14", "نیمه شعبان"),
                ("1404/12/20", "شهادت امام علی"),
            ],
            [1405] =
            [
                ("1405/01/01", "عید فطر"),
                ("1405/01/02", "عید فطر"),
                ("1405/01/25", "شهادت امام جعفر صادق"),
                ("1405/03/06", "عید قربان"),
                ("1405/03/14", "عید غدیر"),
                ("1405/04/03", "تاسوعا"),
                ("1405/04/04", "عاشورا"),
                ("1405/05/13", "اربعین"),
                ("1405/05/21", "رحلت رسول اکرم و شهادت امام حسن"),
                ("1405/05/22", "شهادت امام رضا"),
                ("1405/05/30", "شهادت امام حسن عسکری"),
                ("1405/06/08", "میلاد رسول اکرم و امام جعفر صادق"),
                ("1405/08/22", "شهادت حضرت فاطمه"),
                ("1405/10/02", "ولادت امام علی"),
                ("1405/10/16", "مبعث حضرت رسول اکرم صلی الله علیه و آل و سلم"),
                ("1405/11/04", "نیمه شعبان"),
                ("1405/12/09", "شهادت امام علی"),
                ("1405/12/19", "عید فطر"),
                ("1405/12/20", "عید فطر"),
            ],
            [1406] =
            [
                ("1406/01/14", "شهادت امام جعفر صادق"),
                ("1406/02/27", "عید قربان"),
                ("1406/03/04", "عید غدیر"),
                ("1406/03/24", "تاسوعا"),
                ("1406/03/25", "عاشورا"),
                ("1406/05/03", "اربعین"),
                ("1406/05/11", "رحلت رسول اکرم و شهادت امام حسن"),
                ("1406/05/12", "شهادت امام رضا"),
                ("1406/05/20", "شهادت امام حسن عسکری"),
                ("1406/05/29", "میلاد رسول اکرم و امام جعفر صادق"),
                ("1406/08/12", "شهادت حضرت فاطمه"),
                ("1406/09/21", "ولادت امام علی"),
                ("1406/10/05", "مبعث حضرت رسول اکرم صلی الله علیه و آل و سلم"),
                ("1406/10/23", "نیمه شعبان"),
                ("1406/11/28", "شهادت امام علی"),
                ("1406/12/08", "عید فطر"),
                ("1406/12/09", "عید فطر"),
            ],
        };

    public static IReadOnlyList<int> Years { get; } = LunarDays.Keys.OrderBy(year => year).ToArray();

    public static bool Has(int shamsiYear)
    {
        return LunarDays.ContainsKey(shamsiYear);
    }

    public static IReadOnlyList<OfficialHolidayDay>? For(int shamsiYear)
    {
        if (!LunarDays.TryGetValue(shamsiYear, out IReadOnlyList<(string Shamsi, string Title)>? lunar))
        {
            return null;
        }

        return Merge(SolarDays(shamsiYear), Lunar(shamsiYear, lunar));
    }

    public static IReadOnlyList<OfficialHolidayDay> SolarDays(int shamsiYear)
    {
        var days = new List<OfficialHolidayDay>
        {
            Day(shamsiYear, 1, 1, Nowruz),
            Day(shamsiYear, 1, 2, Nowruz),
            Day(shamsiYear, 1, 3, Nowruz),
            Day(shamsiYear, 1, 4, Nowruz),
            Day(shamsiYear, 1, 12, RepublicDay),
            Day(shamsiYear, 1, 13, NatureDay),
            Day(shamsiYear, 3, 14, Khomeini),
            Day(shamsiYear, 3, 15, Khordad15),
            Day(shamsiYear, 11, 22, Revolution),
            Day(shamsiYear, 12, 29, OilDay),
        };
        if (IsLeap(shamsiYear))
        {
            days.Add(Day(shamsiYear, 12, 30, Nowruz));
        }

        return days;
    }

    private static IReadOnlyList<OfficialHolidayDay> Lunar(
        int shamsiYear,
        IReadOnlyList<(string Shamsi, string Title)> rows)
    {
        var days = new List<OfficialHolidayDay>(rows.Count);
        foreach ((string shamsi, string title) in rows)
        {
            DateOnly date = Dates.ParseShamsiDate(shamsi);
            if (Leave.ShamsiYearOf(date) != shamsiYear)
            {
                throw new InvalidOperationException($"تاریخ {shamsi} در سال {shamsiYear} نیست.");
            }

            days.Add(new OfficialHolidayDay(date, Persian.NormalizeText(title)));
        }

        return days;
    }

    private static IReadOnlyList<OfficialHolidayDay> Merge(
        IReadOnlyList<OfficialHolidayDay> solar,
        IReadOnlyList<OfficialHolidayDay> lunar)
    {
        var map = new Dictionary<string, OfficialHolidayDay>(StringComparer.Ordinal);
        foreach (OfficialHolidayDay day in solar.Concat(lunar))
        {
            string key = Dates.StorageDate(day.Date);
            if (map.TryGetValue(key, out OfficialHolidayDay? existing))
            {
                map[key] = existing with { Title = $"{existing.Title} و {day.Title}" };
            }
            else
            {
                map[key] = day;
            }
        }

        return map.Values.OrderBy(day => day.Date).ToList();
    }

    private static OfficialHolidayDay Day(int year, int month, int day, string title)
    {
        return new OfficialHolidayDay(Dates.ParseShamsiDate($"{year:0000}/{month:00}/{day:00}"), title);
    }

    private static bool IsLeap(int year)
    {
        try
        {
            Dates.ParseShamsiDate($"{year:0000}/12/30");
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
