using System.Globalization;

namespace Atten.Core;

/// <summary>
/// Yearly leave is counted in minutes. One leave day is 7 hours and 20 minutes.
/// A full month earns 2.5 days. At most 9 days carry into the next cooperation year.
/// The year cooperation ends carries nothing. The balance after the transfer is
/// the remainder minus the days that carry.
/// </summary>
public static class Leave
{
    public const int LeaveDayMinutes = 7 * 60 + 20;
    public const int YearlyLeaveDays = 30;
    public const int YearlyLeaveMinutes = YearlyLeaveDays * LeaveDayMinutes;
    public const int MonthlyLeaveMinutes = LeaveDayMinutes * 5 / 2;
    public const int CarryLeaveDays = 9;
    public const int CarryLeaveMinutes = CarryLeaveDays * LeaveDayMinutes;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int ShamsiYearOf(DateOnly value)
    {
        return Dates.ShamsiYmd(value).Year;
    }

    public static int ShamsiYearOf(DateTime value)
    {
        return Dates.ShamsiYmd(value).Year;
    }

    public static int ShamsiYearOf(string value)
    {
        return Dates.ShamsiYmd(value).Year;
    }

    public static (DateOnly Start, DateOnly End) ShamsiYearSpan(int year)
    {
        DateOnly start = Dates.ParseShamsiDate($"{year:0000}/01/01");
        try
        {
            return (start, Dates.ParseShamsiDate($"{year:0000}/12/30"));
        }
        catch (FormatException)
        {
            return (start, Dates.ParseShamsiDate($"{year:0000}/12/29"));
        }
    }

    public static int MonthLeaveMinutes(int coveredDays, int monthLength)
    {
        if (coveredDays <= 0 || monthLength <= 0)
        {
            return 0;
        }

        if (coveredDays >= monthLength)
        {
            return MonthlyLeaveMinutes;
        }

        return (coveredDays * MonthlyLeaveMinutes + monthLength / 2) / monthLength;
    }

    /// <summary>
    /// Minutes earned in one Shamsi year inside the cooperation window.
    /// Both ends of the window are included. Calendar days count, including
    /// Fridays and holidays. An empty end runs through the last day of
    /// <paramref name="year"/>. An empty start earns nothing.
    /// </summary>
    public static int EarnedLeaveMinutes(
        int year,
        DateOnly? cooperationStart,
        DateOnly? cooperationEnd = null)
    {
        if (cooperationStart is null)
        {
            return 0;
        }

        var (yearStart, yearEnd) = ShamsiYearSpan(year);
        DateOnly ended = cooperationEnd ?? yearEnd;
        DateOnly windowStart = Max(cooperationStart.Value, yearStart);
        DateOnly windowEnd = Min(ended, yearEnd);
        if (windowEnd < windowStart)
        {
            return 0;
        }

        int total = 0;
        for (int month = 1; month <= 12; month++)
        {
            IReadOnlyList<DateOnly> monthDays = Dates.ShamsiMonthDates(year, month);
            DateOnly overlapStart = Max(windowStart, monthDays[0]);
            DateOnly overlapEnd = Min(windowEnd, monthDays[^1]);
            if (overlapEnd < overlapStart)
            {
                continue;
            }

            int covered = overlapEnd.DayNumber - overlapStart.DayNumber + 1;
            total += MonthLeaveMinutes(covered, monthDays.Count);
        }

        return total;
    }

    /// <summary>
    /// Settle earned and used minutes from the first cooperation year forward.
    /// Each item is a Shamsi year with earned and used minutes. The same year
    /// may appear more than once; its minutes are added together.
    /// <c>CarryIn</c> is the previous year's <c>CarryOut</c> only when that year
    /// is the immediately previous Shamsi year. A missing year drops the carry.
    /// A positive remainder carries at most 9 days. The year equal to
    /// <paramref name="closingYear"/> carries nothing. A zero or negative
    /// remainder carries nothing.
    /// </summary>
    public static IReadOnlyList<LeaveYearSettlement> SettleLeaveYears(
        IEnumerable<(int Year, int Earned, int Used)> years,
        int? closingYear = null)
    {
        ArgumentNullException.ThrowIfNull(years);

        var combined = new Dictionary<int, (int Earned, int Used)>();
        foreach (var (year, earned, used) in years)
        {
            combined.TryGetValue(year, out var previous);
            combined[year] = (previous.Earned + earned, previous.Used + used);
        }

        int incoming = 0;
        int? previousYear = null;
        var settled = new List<LeaveYearSettlement>(combined.Count);
        foreach (int year in combined.Keys.OrderBy(value => value))
        {
            var (earned, used) = combined[year];
            int carryIn = previousYear is not null && year == previousYear + 1 ? incoming : 0;
            int usable = earned + carryIn;
            int remaining = usable - used;
            int carryOut = remaining > 0 && year != closingYear
                ? Math.Min(remaining, CarryLeaveMinutes)
                : 0;
            settled.Add(new LeaveYearSettlement(year, earned, carryIn, used, usable, remaining, carryOut));
            incoming = carryOut;
            previousYear = year;
        }

        return settled;
    }

    /// <summary>
    /// Minutes still available in one Shamsi year after earlier years settle.
    /// An empty cooperation start earns nothing. Only years that overlap the
    /// cooperation window are settled, so a gap drops the carry. A recorded
    /// cooperation end makes that Shamsi year the closing year, so it carries
    /// nothing forward. The returned minutes are the balance before that
    /// transfer. <paramref name="usedByYear"/> maps a Shamsi year to minutes
    /// of leave that start in that year.
    /// </summary>
    public static int RemainingLeaveMinutes(
        int year,
        DateOnly? cooperationStart,
        DateOnly? cooperationEnd,
        IReadOnlyDictionary<int, int> usedByYear)
    {
        ArgumentNullException.ThrowIfNull(usedByYear);

        int usedHere = usedByYear.GetValueOrDefault(year);
        if (cooperationStart is null || year < ShamsiYearOf(cooperationStart.Value))
        {
            return -usedHere;
        }

        int? closingYear = cooperationEnd is null ? null : ShamsiYearOf(cooperationEnd.Value);
        var entries = new List<(int Year, int Earned, int Used)>();
        for (int candidate = ShamsiYearOf(cooperationStart.Value); candidate <= year; candidate++)
        {
            int earned = EarnedLeaveMinutes(candidate, cooperationStart, cooperationEnd);
            if (earned <= 0)
            {
                continue;
            }

            entries.Add((candidate, earned, usedByYear.GetValueOrDefault(candidate)));
        }

        foreach (LeaveYearSettlement row in SettleLeaveYears(entries, closingYear))
        {
            if (row.Year == year)
            {
                return row.Remaining;
            }
        }

        return -usedHere;
    }

    public static int ComposeLeaveMinutes(int days, int hours, int minutes)
    {
        return days * LeaveDayMinutes + hours * 60 + minutes;
    }

    public static (int Days, int Hours, int Minutes) SplitLeaveMinutes(int minutes)
    {
        int total = Math.Max(minutes, 0);
        int days = Math.DivRem(total, LeaveDayMinutes, out int rest);
        int hours = Math.DivRem(rest, 60, out int mins);
        return (days, hours, mins);
    }

    public static string FormatLeaveAmount(int minutes)
    {
        var (days, hours, mins) = SplitLeaveMinutes(minutes);
        return $"{days} روز و {hours} ساعت و {mins} دقیقه";
    }

    public static string FormatSignedLeaveAmount(int minutes)
    {
        return minutes < 0 ? $"-{FormatLeaveAmount(-minutes)}" : FormatLeaveAmount(minutes);
    }

    /// <summary>
    /// Shamsi years from cooperation start through its end.
    /// An empty end stops at the current Shamsi year. The current year is always
    /// included. Newer years come first.
    /// </summary>
    public static IReadOnlyList<int> CooperationYearChoices(
        DateOnly? cooperationStart,
        DateOnly? cooperationEnd = null,
        DateOnly? today = null)
    {
        int current = ShamsiYearOf(today ?? DateOnly.FromDateTime(DateTime.Today));
        var years = new HashSet<int> { current };
        if (cooperationStart is { } started)
        {
            int startYear = ShamsiYearOf(started);
            int endYear = cooperationEnd is { } ended ? ShamsiYearOf(ended) : current;
            int from = Math.Min(startYear, endYear);
            int to = Math.Max(startYear, endYear);
            for (int year = from; year <= to; year++)
            {
                years.Add(year);
            }
        }

        return years.OrderByDescending(year => year).ToList();
    }

    /// <summary>
    /// Settle earned and used minutes for each cooperation year that earns leave.
    /// An empty start returns no rows. A recorded end makes that Shamsi year
    /// the closing year.
    /// </summary>
    public static IReadOnlyDictionary<int, LeaveYearSettlement> SettleCooperationYears(
        DateOnly? cooperationStart,
        DateOnly? cooperationEnd,
        IReadOnlyDictionary<int, int> usedByYear,
        DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(usedByYear);
        if (cooperationStart is null)
        {
            return new Dictionary<int, LeaveYearSettlement>();
        }

        var entries = new List<(int Year, int Earned, int Used)>();
        foreach (int year in CooperationYearChoices(cooperationStart, cooperationEnd, today).OrderBy(value => value))
        {
            int earned = EarnedLeaveMinutes(year, cooperationStart, cooperationEnd);
            if (earned <= 0)
            {
                continue;
            }

            entries.Add((year, earned, usedByYear.GetValueOrDefault(year)));
        }

        int? closingYear = cooperationEnd is null ? null : ShamsiYearOf(cooperationEnd.Value);
        return SettleLeaveYears(entries, closingYear).ToDictionary(row => row.Year);
    }

    public static LeaveYearBalance YearBalance(
        int year,
        DateOnly? cooperationStart,
        IReadOnlyDictionary<int, LeaveYearSettlement> settled,
        IReadOnlyDictionary<int, int> usedByYear)
    {
        ArgumentNullException.ThrowIfNull(settled);
        ArgumentNullException.ThrowIfNull(usedByYear);

        int used = usedByYear.GetValueOrDefault(year);
        if (cooperationStart is null)
        {
            return new LeaveYearBalance(year, 0, 0, 0, 0, 0, 0, MissingStart: true);
        }

        if (!settled.TryGetValue(year, out LeaveYearSettlement? row))
        {
            return new LeaveYearBalance(year, 0, 0, used, -used, -used, 0, MissingStart: false);
        }

        return new LeaveYearBalance(
            year,
            row.CarryIn,
            row.Earned,
            row.Used,
            row.Remaining,
            row.Remaining - row.CarryOut,
            row.CarryOut,
            MissingStart: false);
    }

    public static string FormatLeaveDuration(int minutes)
    {
        if (minutes <= 0)
        {
            return "";
        }

        int hours = Math.DivRem(minutes, 60, out int mins);
        if (hours != 0 && mins != 0)
        {
            return $"{hours} ساعت و {mins} دقیقه";
        }

        if (hours != 0)
        {
            return $"{hours} ساعت";
        }

        return $"{mins} دقیقه";
    }

    /// <summary>Split each leave across the working days it covers.</summary>
    public static IReadOnlyDictionary<string, int> SpreadLeaveMinutes(
        IEnumerable<LeaveSpan> leaves,
        Func<DateOnly, bool> isHoliday)
    {
        ArgumentNullException.ThrowIfNull(leaves);
        ArgumentNullException.ThrowIfNull(isHoliday);

        var credit = new Dictionary<string, int>();
        foreach (LeaveSpan leave in leaves)
        {
            if (leave.Minutes <= 0 || leave.EndDate < leave.StartDate)
            {
                continue;
            }

            var working = new List<string>();
            for (DateOnly current = leave.StartDate; current <= leave.EndDate; current = current.AddDays(1))
            {
                if (!isHoliday(current))
                {
                    working.Add(Dates.StorageDate(current));
                }
            }

            if (working.Count == 0)
            {
                continue;
            }

            int share = Math.DivRem(leave.Minutes, working.Count, out int extra);
            for (int index = 0; index < working.Count; index++)
            {
                string key = working[index];
                credit[key] = credit.GetValueOrDefault(key) + share + (index < extra ? 1 : 0);
            }
        }

        return credit;
    }

    /// <summary>Null or blank text is no date. Stored dates are Gregorian <c>YYYY-MM-DD</c>.</summary>
    public static DateOnly? OptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return AsDate(value);
    }

    private static DateOnly AsDate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string text = value.Length >= 10 ? value[..10] : value;
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out DateOnly day))
        {
            throw new FormatException("تاریخ معتبر نیست.");
        }

        return day;
    }

    private static DateOnly Max(DateOnly left, DateOnly right)
    {
        return left >= right ? left : right;
    }

    private static DateOnly Min(DateOnly left, DateOnly right)
    {
        return left <= right ? left : right;
    }
}

public sealed record LeaveYearSettlement(
    int Year,
    int Earned,
    int CarryIn,
    int Used,
    int Usable,
    int Remaining,
    int CarryOut);

public sealed record LeaveYearBalance(
    int Year,
    int Carry,
    int Earned,
    int Used,
    int RemainingBeforeTransfer,
    int RemainingAfterTransfer,
    int Transfer,
    bool MissingStart);

public sealed record LeaveSpan(DateOnly StartDate, DateOnly EndDate, int Minutes);
