using System.Globalization;

namespace Atten.Core;

/// <summary>
/// Shamsi on screen, Gregorian in the database and on the wire.
/// Stored dates are text: YYYY-MM-DD.
/// Stored datetimes are text: YYYY-MM-DDTHH:MM:SS.
/// Both are Gregorian. Time is the civil clock time, with no timezone shift.
/// </summary>
public static class Dates
{
    public static readonly IReadOnlyList<string> ShamsiMonthNames =
    [
        "فروردین",
        "اردیبهشت",
        "خرداد",
        "تیر",
        "مرداد",
        "شهریور",
        "مهر",
        "آبان",
        "آذر",
        "دی",
        "بهمن",
        "اسفند",
    ];

    public static readonly IReadOnlyList<string> ShamsiWeekdayNames =
    [
        "شنبه",
        "یکشنبه",
        "دوشنبه",
        "سه‌شنبه",
        "چهارشنبه",
        "پنجشنبه",
        "جمعه",
    ];

    private const string InvalidDate = "تاریخ شمسی معتبر نیست.";
    private const string InvalidTime = "ساعت معتبر نیست.";
    private static readonly int[] GregorianMonthDays = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    private static readonly int[] JalaliMonthDays = [31, 31, 31, 31, 31, 31, 30, 30, 30, 30, 30, 29];
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string StorageDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", Invariant);
    }

    public static string StorageDateTime(DateTime value)
    {
        return value.ToString("yyyy-MM-ddTHH:mm:ss", Invariant);
    }

    public static string FormatShamsiDate(DateOnly value)
    {
        var (year, month, day) = GregorianToJalali(value.Year, value.Month, value.Day);
        return $"{year:0000}/{month:00}/{day:00}";
    }

    public static string FormatShamsiDate(DateTime value)
    {
        return FormatShamsiDate(DateOnly.FromDateTime(value));
    }

    public static string FormatShamsiDate(string value)
    {
        return FormatShamsiDate(AsDate(value));
    }

    public static string FormatShamsiDateTime(DateTime value, bool seconds = false)
    {
        string clock = seconds
            ? value.ToString("HH:mm:ss", Invariant)
            : FormatClock(value);
        return $"{FormatShamsiDate(value)} {clock}";
    }

    public static string FormatShamsiDateTime(string value, bool seconds = false)
    {
        return FormatShamsiDateTime(AsDateTime(value), seconds);
    }

    public static DateOnly ParseShamsiDate(string text)
    {
        var (year, month, day) = ShamsiParts(text);
        return JalaliToDate(year, month, day);
    }

    public static DateTime ParseShamsiDateTime(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string cleaned = Normalize(text);
        if (cleaned.Length == 0)
        {
            throw new FormatException(InvalidDate);
        }

        int separator = cleaned.IndexOf(' ');
        if (separator < 0 || separator == cleaned.Length - 1)
        {
            throw new FormatException(InvalidTime);
        }

        DateOnly gregorian = ParseShamsiDate(cleaned[..separator]);
        var (hour, minute, second) = ClockParts(cleaned[(separator + 1)..]);
        return new DateTime(gregorian.Year, gregorian.Month, gregorian.Day, hour, minute, second, DateTimeKind.Unspecified);
    }

    public static (int Year, int Month, int Day) ShamsiYmd(DateOnly value)
    {
        return GregorianToJalali(value.Year, value.Month, value.Day);
    }

    public static (int Year, int Month, int Day) ShamsiYmd(DateTime value)
    {
        return ShamsiYmd(DateOnly.FromDateTime(value));
    }

    public static (int Year, int Month, int Day) ShamsiYmd(string value)
    {
        return ShamsiYmd(AsDate(value));
    }

    /// <summary>Saturday is 0 and Friday is 6.</summary>
    public static int ShamsiWeekIndex(DateOnly value)
    {
        return ((int)value.DayOfWeek + 1) % 7;
    }

    public static int ShamsiWeekIndex(DateTime value)
    {
        return ShamsiWeekIndex(DateOnly.FromDateTime(value));
    }

    public static int ShamsiWeekIndex(string value)
    {
        return ShamsiWeekIndex(AsDate(value));
    }

    public static string FormatShamsiWeekday(DateOnly value)
    {
        return ShamsiWeekdayNames[ShamsiWeekIndex(value)];
    }

    public static string FormatShamsiWeekday(DateTime value)
    {
        return FormatShamsiWeekday(DateOnly.FromDateTime(value));
    }

    public static string FormatShamsiWeekday(string value)
    {
        return FormatShamsiWeekday(AsDate(value));
    }

    public static IReadOnlyList<DateOnly> ShamsiMonthDates(int year, int month)
    {
        if (year < 1 || month < 1 || month > 12)
        {
            throw new FormatException(InvalidDate);
        }

        DateOnly current = ParseShamsiDate($"{year:0000}/{month:00}/01");
        var days = new List<DateOnly>();
        while (true)
        {
            var (shownYear, shownMonth, _) = ShamsiYmd(current);
            if (shownYear != year || shownMonth != month)
            {
                break;
            }

            days.Add(current);
            current = current.AddDays(1);
            if (days.Count > 31)
            {
                throw new FormatException(InvalidDate);
            }
        }

        return days;
    }

    private static (int Year, int Month, int Day) GregorianToJalali(int year, int month, int day)
    {
        int gy = year - 1600;
        int gm = month - 1;
        int dayNumber = 365 * gy + (gy + 3) / 4 - (gy + 99) / 100 + (gy + 399) / 400;
        for (int i = 0; i < gm; i++)
        {
            dayNumber += GregorianMonthDays[i];
        }

        dayNumber += day - 80;
        if (gm > 1 && GregorianLeap(year))
        {
            dayNumber += 1;
        }

        int cycles = Math.DivRem(dayNumber, 12053, out dayNumber);
        int jy = 979 + 33 * cycles + 4 * (dayNumber / 1461);
        dayNumber %= 1461;
        if (dayNumber >= 366)
        {
            dayNumber -= 1;
            jy += dayNumber / 365;
            dayNumber %= 365;
        }

        for (int index = 0; index < JalaliMonthDays.Length - 1; index++)
        {
            int length = JalaliMonthDays[index];
            if (dayNumber < length)
            {
                return (jy, index + 1, dayNumber + 1);
            }

            dayNumber -= length;
        }

        return (jy, 12, dayNumber + 1);
    }

    private static DateOnly JalaliToDate(int year, int month, int day)
    {
        if (month < 1 || month > 12 || day < 1 || day > 31)
        {
            throw new FormatException(InvalidDate);
        }

        int gy = year - 979;
        int dayNumber = 365 * gy + (gy / 33) * 8 + (gy % 33 + 3) / 4 + 78 + day;
        for (int i = 0; i < month - 1; i++)
        {
            dayNumber += JalaliMonthDays[i];
        }

        gy = 1600 + 400 * Math.DivRem(dayNumber, 146097, out dayNumber);
        bool leap = true;
        if (dayNumber >= 36525)
        {
            dayNumber -= 1;
            gy += 100 * Math.DivRem(dayNumber, 36524, out dayNumber);
            if (dayNumber >= 365)
            {
                dayNumber += 1;
            }
            else
            {
                leap = false;
            }
        }

        gy += 4 * Math.DivRem(dayNumber, 1461, out dayNumber);
        if (dayNumber >= 366)
        {
            leap = false;
            dayNumber -= 1;
            gy += Math.DivRem(dayNumber, 365, out dayNumber);
        }

        int monthIndex = 0;
        while (monthIndex < 12)
        {
            int length = GregorianMonthDays[monthIndex] + (monthIndex == 1 && leap ? 1 : 0);
            if (dayNumber < length)
            {
                break;
            }

            dayNumber -= length;
            monthIndex++;
        }

        if (monthIndex >= 12)
        {
            throw new FormatException(InvalidDate);
        }

        DateOnly gregorian;
        try
        {
            gregorian = new DateOnly(gy, monthIndex + 1, dayNumber + 1);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new FormatException(InvalidDate, ex);
        }

        var (shownYear, shownMonth, shownDay) = GregorianToJalali(gregorian.Year, gregorian.Month, gregorian.Day);
        if (shownYear != year || shownMonth != month || shownDay != day)
        {
            throw new FormatException(InvalidDate);
        }

        return gregorian;
    }

    private static bool GregorianLeap(int year)
    {
        return year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    }

    private static DateOnly AsDate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string text = Normalize(value).Split(' ')[0].Replace('/', '-');
        string iso = text.Length >= 10 ? text[..10] : text;
        if (!DateOnly.TryParseExact(iso, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out DateOnly day))
        {
            throw new FormatException(InvalidDate);
        }

        return day;
    }

    private static DateTime AsDateTime(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string text = Normalize(value).Replace('/', '-');
        if (!TrySplit(text, 'T', out string dateText, out string timeText)
            && !TrySplit(text, ' ', out dateText, out timeText))
        {
            throw new FormatException(InvalidTime);
        }

        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out DateOnly gregorian))
        {
            throw new FormatException(InvalidDate);
        }

        var (hour, minute, second) = ClockParts(timeText);
        return new DateTime(gregorian.Year, gregorian.Month, gregorian.Day, hour, minute, second, DateTimeKind.Unspecified);
    }

    private static (int Year, int Month, int Day) ShamsiParts(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string cleaned = Normalize(text).Split(' ')[0].Replace('-', '/').Replace('.', '/');
        string[] pieces = cleaned.Split('/');
        if (pieces.Length != 3 || !pieces.All(IsAsciiDigits))
        {
            throw new FormatException(InvalidDate);
        }

        int year = int.Parse(pieces[0], Invariant);
        int month = int.Parse(pieces[1], Invariant);
        int day = int.Parse(pieces[2], Invariant);
        if (year < 1)
        {
            throw new FormatException(InvalidDate);
        }

        return (year, month, day);
    }

    private static (int Hour, int Minute, int Second) ClockParts(string text)
    {
        string[] pieces = text.Split(':');
        if (pieces.Length is not (2 or 3) || !pieces.All(IsAsciiDigits))
        {
            throw new FormatException(InvalidTime);
        }

        int hour = int.Parse(pieces[0], Invariant);
        int minute = int.Parse(pieces[1], Invariant);
        int second = pieces.Length == 3 ? int.Parse(pieces[2], Invariant) : 0;
        if (hour > 23 || minute > 59 || second > 59)
        {
            throw new FormatException(InvalidTime);
        }

        return (hour, minute, second);
    }

    private static string FormatClock(DateTime value)
    {
        return value.Second != 0
            ? value.ToString("HH:mm:ss", Invariant)
            : value.ToString("HH:mm", Invariant);
    }

    private static string Normalize(string text)
    {
        return string.Join(
            " ",
            Persian.EnglishDigits(text.Trim()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsAsciiDigits(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySplit(string text, char separator, out string left, out string right)
    {
        int index = text.IndexOf(separator);
        if (index < 0 || index == text.Length - 1)
        {
            left = "";
            right = "";
            return false;
        }

        left = text[..index];
        right = text[(index + 1)..];
        return true;
    }
}
