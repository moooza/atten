using Xunit;

namespace Atten.Core.Tests;

public class OfficialHolidayYearPackTests
{
    [Fact]
    public void SolarDaysPinTheFixedShamsiOfficialDates()
    {
        IReadOnlyList<OfficialHolidayDay> days = OfficialHolidayYearPack.SolarDays(1405);

        Assert.Equal(
            [
                ("1405/01/01", OfficialHolidayYearPack.Nowruz),
                ("1405/01/02", OfficialHolidayYearPack.Nowruz),
                ("1405/01/03", OfficialHolidayYearPack.Nowruz),
                ("1405/01/04", OfficialHolidayYearPack.Nowruz),
                ("1405/01/12", OfficialHolidayYearPack.RepublicDay),
                ("1405/01/13", OfficialHolidayYearPack.NatureDay),
                ("1405/03/14", OfficialHolidayYearPack.Khomeini),
                ("1405/03/15", OfficialHolidayYearPack.Khordad15),
                ("1405/11/22", OfficialHolidayYearPack.Revolution),
                ("1405/12/29", OfficialHolidayYearPack.OilDay),
            ],
            days.Select(day => (Dates.FormatShamsiDate(day.Date), day.Title)));
        Assert.Equal(new DateOnly(2026, 3, 21), days[0].Date);
        Assert.Equal(new DateOnly(2026, 4, 2), days.Single(day => day.Title == OfficialHolidayYearPack.NatureDay).Date);
        Assert.DoesNotContain(days, day => Dates.FormatShamsiDate(day.Date) == "1405/12/30");
    }

    [Fact]
    public void SolarDaysAddsTheLeapNowruzDay()
    {
        IReadOnlyList<OfficialHolidayDay> days = OfficialHolidayYearPack.SolarDays(1403);
        OfficialHolidayDay extra = Assert.Single(
            days,
            day => Dates.FormatShamsiDate(day.Date) == "1403/12/30");
        Assert.Equal(OfficialHolidayYearPack.Nowruz, extra.Title);
        Assert.Equal(new DateOnly(2025, 3, 20), extra.Date);
    }

    [Fact]
    public void YearPackIsOnlyOfferedForCuratedYears()
    {
        Assert.Equal([1404, 1405, 1406], OfficialHolidayYearPack.Years);
        Assert.True(OfficialHolidayYearPack.Has(1405));
        Assert.False(OfficialHolidayYearPack.Has(1403));
        Assert.False(OfficialHolidayYearPack.Has(1410));
        Assert.Null(OfficialHolidayYearPack.For(1403));
    }

    [Fact]
    public void YearPackMergesALunarDayThatLandsOnASolarDate()
    {
        IReadOnlyList<OfficialHolidayDay> pack = OfficialHolidayYearPack.For(1405)!;
        OfficialHolidayDay nowruz = pack.Single(day => day.Date == Dates.ParseShamsiDate("1405/01/01"));
        OfficialHolidayDay republic = pack.Single(day => day.Date == Dates.ParseShamsiDate("1405/01/12"));
        OfficialHolidayDay khomeini = pack.Single(day => day.Date == Dates.ParseShamsiDate("1405/03/14"));

        Assert.Equal("نوروز و عید فطر", nowruz.Title);
        Assert.Equal(OfficialHolidayYearPack.RepublicDay, republic.Title);
        Assert.Equal("رحلت امام خمینی و عید غدیر", khomeini.Title);
    }

    [Fact]
    public void YearPackKeepsShiftingDaysEditableAndInsideThatShamsiYear()
    {
        IReadOnlyList<OfficialHolidayDay> pack = OfficialHolidayYearPack.For(1405)!;
        OfficialHolidayDay ashura = pack.Single(day => day.Title == "عاشورا");
        OfficialHolidayDay tasua = pack.Single(day => day.Title == "تاسوعا");
        OfficialHolidayDay qurban = pack.Single(day => day.Title == "عید قربان");

        Assert.Equal(Dates.ParseShamsiDate("1405/04/04"), ashura.Date);
        Assert.Equal(new DateOnly(2026, 6, 25), ashura.Date);
        Assert.Equal(Dates.ParseShamsiDate("1405/04/03"), tasua.Date);
        Assert.Equal(Dates.ParseShamsiDate("1405/03/06"), qurban.Date);
        Assert.All(pack, day => Assert.Equal(1405, Leave.ShamsiYearOf(day.Date)));
        Assert.Equal(pack.Count, pack.Select(day => Dates.StorageDate(day.Date)).Distinct().Count());
        Assert.Equal(pack.Select(day => day.Date), pack.Select(day => day.Date).OrderBy(day => day));
    }

    [Fact]
    public void YearPack1404PinsKnownPastLunarDays()
    {
        IReadOnlyList<OfficialHolidayDay> pack = OfficialHolidayYearPack.For(1404)!;
        OfficialHolidayDay ashura = pack.Single(day => day.Title == "عاشورا");
        OfficialHolidayDay fitr = pack.Single(day => Dates.FormatShamsiDate(day.Date) == "1404/01/11");
        OfficialHolidayDay republic = pack.Single(day => day.Date == Dates.ParseShamsiDate("1404/01/12"));

        Assert.Equal(new DateOnly(2025, 7, 6), ashura.Date);
        Assert.Equal("عید فطر", fitr.Title);
        Assert.Equal("روز جمهوری اسلامی و عید فطر", republic.Title);
        Assert.All(pack, day => Assert.Equal(1404, Leave.ShamsiYearOf(day.Date)));
    }

    [Fact]
    public void YearPack1406StaysInsideThatYear()
    {
        IReadOnlyList<OfficialHolidayDay> pack = OfficialHolidayYearPack.For(1406)!;
        OfficialHolidayDay ashura = pack.Single(day => day.Title == "عاشورا");

        Assert.Equal(Dates.ParseShamsiDate("1406/03/25"), ashura.Date);
        Assert.All(pack, day => Assert.Equal(1406, Leave.ShamsiYearOf(day.Date)));
        Assert.Contains(pack, day => day.Title == OfficialHolidayYearPack.OilDay);
    }
}
