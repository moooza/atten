using Xunit;

namespace Atten.Core.Tests;

public class DatesTests
{
    [Fact]
    public void KnownCivilDatesConvertBothWays()
    {
        (DateOnly Gregorian, string Shamsi)[] pairs =
        [
            (new DateOnly(2026, 10, 7), "1405/07/15"),
            (new DateOnly(2026, 3, 21), "1405/01/01"),
            (new DateOnly(2025, 3, 21), "1404/01/01"),
            (new DateOnly(2025, 3, 20), "1403/12/30"),
            (new DateOnly(2024, 3, 20), "1403/01/01"),
            (new DateOnly(1979, 2, 11), "1357/11/22"),
        ];

        foreach (var (gregorian, shamsi) in pairs)
        {
            Assert.Equal(shamsi, Dates.FormatShamsiDate(gregorian));
            Assert.Equal(shamsi, Dates.FormatShamsiDate(Dates.StorageDate(gregorian)));
            Assert.Equal(gregorian, Dates.ParseShamsiDate(shamsi));
            Assert.Equal(Dates.StorageDate(gregorian), Dates.StorageDate(Dates.ParseShamsiDate(shamsi)));
        }
    }

    [Fact]
    public void ShamsiInputAcceptsPersianDigitsAndStoresGregorianDateTime()
    {
        DateTime moment = Dates.ParseShamsiDateTime("۱۴۰۵/۰۷/۱۵ ۱۶:۳۰");
        Assert.Equal(new DateTime(2026, 10, 7, 16, 30, 0), moment);
        Assert.Equal("2026-10-07T16:30:00", Dates.StorageDateTime(moment));
        Assert.Equal("1405/07/15 16:30", Dates.FormatShamsiDateTime(Dates.StorageDateTime(moment)));
        Assert.Equal("1405/07/15 16:30:00", Dates.FormatShamsiDateTime(Dates.StorageDateTime(moment), seconds: true));
    }

    [Fact]
    public void ShamsiMonthStartsOnSaturdayIndexAndListsMehr1405()
    {
        IReadOnlyList<DateOnly> days = Dates.ShamsiMonthDates(1405, 7);
        Assert.Equal(30, days.Count);
        Assert.Equal(new DateOnly(2026, 9, 23), days[0]);
        Assert.Equal(new DateOnly(2026, 10, 7), days[14]);
        Assert.Equal("1405/07/30", Dates.FormatShamsiDate(days[^1]));
        Assert.Equal(4, Dates.ShamsiWeekIndex(new DateOnly(2026, 10, 7)));
        Assert.Equal("چهارشنبه", Dates.FormatShamsiWeekday(new DateOnly(2026, 10, 7)));
        Assert.Equal("جمعه", Dates.FormatShamsiWeekday("2026-10-09"));
        Assert.Equal("شنبه", Dates.FormatShamsiWeekday("2026-10-10"));
        Assert.Equal((new DateOnly(2026, 9, 23), new DateOnly(2026, 10, 22)), Dates.ShamsiMonthSpan(1405, 7));
        Assert.Equal("مهر 1405", Dates.FormatShamsiMonth(1405, 7));
        Assert.Equal("مهر 1405", Dates.FormatShamsiMonth(new DateOnly(2026, 10, 7)));
    }

    [Fact]
    public void ShamsiInputMaskInsertsSlashesAndKeepsEightDigits()
    {
        Assert.Equal(string.Empty, Dates.FormatShamsiInput(""));
        Assert.Equal("140", Dates.FormatShamsiInput("140"));
        Assert.Equal("1405/", Dates.FormatShamsiInput("1405"));
        Assert.Equal("1405/0", Dates.FormatShamsiInput("14050"));
        Assert.Equal("1405/07/", Dates.FormatShamsiInput("140507"));
        Assert.Equal("1405/07/1", Dates.FormatShamsiInput("1405071"));
        Assert.Equal("1405/07/15", Dates.FormatShamsiInput("14050715"));
        Assert.Equal("1405/07/15", Dates.FormatShamsiInput("1405/07/15"));
        Assert.Equal("1405/07/15", Dates.FormatShamsiInput("۱۴۰۵۰۷۱۵"));
        Assert.Equal("1405/07/15", Dates.FormatShamsiInput("1405-07-15 extra"));
        Assert.Equal("1405/07/15", Dates.FormatShamsiInput("1405071599"));
        Assert.Equal("1405/", Dates.FormatShamsiInput("1405/"));
        Assert.Equal("1405", Dates.ShamsiInputDigits("۱۴۰۵/"));
    }

    [Fact]
    public void ClockInputMaskInsertsColonsEveryTwoDigits()
    {
        Assert.Equal(string.Empty, Dates.FormatClockInput(""));
        Assert.Equal("0", Dates.FormatClockInput("0"));
        Assert.Equal("08:", Dates.FormatClockInput("08"));
        Assert.Equal("08:3", Dates.FormatClockInput("083"));
        Assert.Equal("08:30:", Dates.FormatClockInput("0830"));
        Assert.Equal("08:30:0", Dates.FormatClockInput("08300"));
        Assert.Equal("08:30:00", Dates.FormatClockInput("083000"));
        Assert.Equal("08:30:00", Dates.FormatClockInput("08:30:00"));
        Assert.Equal("08:30:00", Dates.FormatClockInput("۰۸۳۰۰۰"));
        Assert.Equal("08:30:00", Dates.FormatClockInput("08300099"));
        Assert.Equal("08:", Dates.FormatClockInput("08:"));
        Assert.Equal("08", Dates.ClockInputDigits("۰۸:"));
    }

    [Fact]
    public void InclusiveDaysKeepsStartAndEndAndRejectsAReversedRange()
    {
        DateOnly start = Dates.ParseShamsiDate("1405/01/19");
        DateOnly end = Dates.ParseShamsiDate("1405/01/21");
        Assert.Equal(
            [start, start.AddDays(1), end],
            Dates.InclusiveDays(start, end));
        Assert.Equal([start], Dates.InclusiveDays(start, start));

        ArgumentException reversed = Assert.Throws<ArgumentException>(
            () => Dates.InclusiveDays(end, start));
        Assert.Equal("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.", reversed.Message);
    }

    [Fact]
    public void InvalidShamsiDatesAreRejected()
    {
        var invalidDate = Assert.Throws<FormatException>(() => Dates.ParseShamsiDate("1404/12/30"));
        Assert.Contains("تاریخ شمسی معتبر نیست", invalidDate.Message);

        var invalidTime = Assert.Throws<FormatException>(() => Dates.ParseShamsiDateTime("1405/07/15"));
        Assert.Contains("ساعت معتبر نیست", invalidTime.Message);
    }
}
