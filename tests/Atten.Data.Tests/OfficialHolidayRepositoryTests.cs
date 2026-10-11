using Atten.Core;
using Xunit;

namespace Atten.Data.Tests;

public class OfficialHolidayRepositoryTests : IDisposable
{
    public OfficialHolidayRepositoryTests()
    {
        StorageClock.Reset();
    }

    public void Dispose()
    {
        StorageClock.Reset();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ListKeepsEachShamsiYearOnTheSameGregorianTable()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly nowruz1404 = Dates.ParseShamsiDate("1404/01/01");
        DateOnly nowruz1405 = Dates.ParseShamsiDate("1405/01/01");
        DateOnly sizdah = Dates.ParseShamsiDate("1405/01/13");
        long nowruzId;
        using (StorageClock.Freeze("2026-10-07T16:30:00"))
        {
            store.OfficialHolidays.Add(nowruz1404, "نوروز");
            nowruzId = store.OfficialHolidays.Add(nowruz1405, "نوروز");
            store.OfficialHolidays.Add(sizdah, "سیزده‌به‌در");
        }

        var (start1405, end1405) = Leave.ShamsiYearSpan(1405);
        IReadOnlyList<OfficialHolidayRecord> year1405 = store.OfficialHolidays.List(start1405, end1405);
        Assert.Equal(
            [
                new OfficialHolidayRecord(
                    nowruzId,
                    Dates.StorageDate(nowruz1405),
                    "نوروز",
                    "2026-10-07T16:30:00",
                    "2026-10-07T16:30:00",
                    DefaultUser.Id,
                    DefaultUser.Id),
                new OfficialHolidayRecord(
                    nowruzId + 1,
                    Dates.StorageDate(sizdah),
                    "سیزده‌به‌در",
                    "2026-10-07T16:30:00",
                    "2026-10-07T16:30:00",
                    DefaultUser.Id,
                    DefaultUser.Id),
            ],
            year1405);

        var (start1404, end1404) = Leave.ShamsiYearSpan(1404);
        OfficialHolidayRecord previous = Assert.Single(store.OfficialHolidays.List(start1404, end1404));
        Assert.Equal(Dates.StorageDate(nowruz1404), previous.Date);
        Assert.Equal(1404, Dates.ShamsiYmd(previous.Date).Year);
        Assert.Equal(1405, Dates.ShamsiYmd(year1405[0].Date).Year);

        IReadOnlySet<string> catalog = OfficialHoliday.Catalog(year1405.Select(row => row.Date));
        Assert.True(OfficialHoliday.IsHoliday(nowruz1405, catalog));
        Assert.False(OfficialHoliday.Contains(catalog, nowruz1404));
    }

    [Fact]
    public void ListWithoutARangeReturnsEveryYearForTheCalendarCatalog()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly nowruz1404 = Dates.ParseShamsiDate("1404/01/01");
        DateOnly nowruz1405 = Dates.ParseShamsiDate("1405/01/01");
        DateOnly sizdah = Dates.ParseShamsiDate("1405/01/13");
        store.OfficialHolidays.Add(nowruz1404, "نوروز");
        store.OfficialHolidays.Add(nowruz1405, "نوروز");
        store.OfficialHolidays.Add(sizdah, "سیزده‌به‌در");

        IReadOnlyList<OfficialHolidayRecord> allYears = store.OfficialHolidays.List();
        Assert.Equal(
            [Dates.StorageDate(nowruz1404), Dates.StorageDate(nowruz1405), Dates.StorageDate(sizdah)],
            allYears.Select(row => row.Date));

        IReadOnlySet<string> catalog = OfficialHoliday.Catalog(allYears.Select(row => row.Date));
        Assert.True(OfficialHoliday.Contains(catalog, nowruz1404));
        Assert.True(OfficialHoliday.Contains(catalog, nowruz1405));
        Assert.True(OfficialHoliday.Contains(catalog, sizdah));
    }

    [Fact]
    public void AddNormalizesTheTitleAndRejectsABlankOrDuplicateDay()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly day = Dates.ParseShamsiDate("1405/01/01");
        store.OfficialHolidays.Add(day, "عاشورا\u064a");

        OfficialHolidayRecord row = Assert.Single(
            store.OfficialHolidays.List(day, day));
        Assert.Equal("عاشورا\u06cc", row.Title);

        ArgumentException blank = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.Add(Dates.ParseShamsiDate("1405/01/02"), "  "));
        Assert.Equal("عنوان تعطیل را وارد کنید.", blank.Message);

        ArgumentException duplicate = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.Add(day, "نوروز"));
        Assert.Equal("این تاریخ قبلاً به‌عنوان تعطیل رسمی ثبت شده است.", duplicate.Message);
        Assert.Single(store.OfficialHolidays.List(day, day));
    }

    [Fact]
    public void UpdateChangesTheRowAndRejectsADuplicateDay()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly nowruz = Dates.ParseShamsiDate("1405/01/01");
        DateOnly sizdah = Dates.ParseShamsiDate("1405/01/13");
        DateOnly second = Dates.ParseShamsiDate("1405/01/02");
        long nowruzId;
        using (StorageClock.Freeze("2026-10-07T16:30:00"))
        {
            nowruzId = store.OfficialHolidays.Add(nowruz, "نوروز");
            store.OfficialHolidays.Add(sizdah, "سیزده‌به‌در");
        }

        using (StorageClock.Freeze("2026-10-08T09:00:00"))
        {
            store.OfficialHolidays.Update(nowruzId, nowruz, "آغاز نوروز");
        }

        OfficialHolidayRecord row = Assert.Single(store.OfficialHolidays.List(nowruz, nowruz));
        Assert.Equal("آغاز نوروز", row.Title);
        Assert.Equal("2026-10-07T16:30:00", row.CreatedAt);
        Assert.Equal("2026-10-08T09:00:00", row.UpdatedAt);

        ArgumentException duplicate = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.Update(nowruzId, sizdah, "نوروز"));
        Assert.Equal("این تاریخ قبلاً به‌عنوان تعطیل رسمی ثبت شده است.", duplicate.Message);
        Assert.Equal(Dates.StorageDate(nowruz), store.OfficialHolidays.List(nowruz, nowruz).Single().Date);

        store.OfficialHolidays.Update(nowruzId, second, "نوروز");
        OfficialHolidayRecord moved = Assert.Single(store.OfficialHolidays.List(second, second));
        Assert.Equal(nowruzId, moved.Id);
        Assert.Equal(Dates.StorageDate(second), moved.Date);

        ArgumentException blank = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.Update(nowruzId, second, "  "));
        Assert.Equal("عنوان تعطیل را وارد کنید.", blank.Message);

        ArgumentException missing = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.Update(0, second, "نوروز"));
        Assert.Equal("یک تعطیل رسمی را انتخاب کنید.", missing.Message);

        ArgumentException gone = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.Update(nowruzId + 99, second, "نوروز"));
        Assert.Equal("این تعطیل رسمی دیگر وجود ندارد.", gone.Message);
    }

    [Fact]
    public void AddManyWritesEveryInclusiveDayAndRollsBackADuplicate()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly start = Dates.ParseShamsiDate("1405/01/19");
        DateOnly end = Dates.ParseShamsiDate("1405/01/21");
        IReadOnlyList<DateOnly> days = Dates.InclusiveDays(start, end);
        store.OfficialHolidays.AddMany(days, "تعطیلات نوروز");

        Assert.Equal(
            days.Select(Dates.StorageDate),
            store.OfficialHolidays.List(start, end).Select(row => row.Date));
        Assert.All(store.OfficialHolidays.List(start, end), row => Assert.Equal("تعطیلات نوروز", row.Title));

        ArgumentException duplicate = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.AddMany(days, "تکراری"));
        Assert.Equal("این تاریخ قبلاً به‌عنوان تعطیل رسمی ثبت شده است.", duplicate.Message);
        Assert.Equal(3, store.OfficialHolidays.List(start, end).Count);

        ArgumentException empty = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.AddMany([], "نوروز"));
        Assert.Equal("حداقل یک تاریخ را انتخاب کنید.", empty.Message);
    }

    [Fact]
    public void DeleteManyRemovesOnlyTheChosenRows()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly nowruz = Dates.ParseShamsiDate("1405/01/01");
        DateOnly second = Dates.ParseShamsiDate("1405/01/02");
        DateOnly sizdah = Dates.ParseShamsiDate("1405/01/13");
        long nowruzId = store.OfficialHolidays.Add(nowruz, "نوروز");
        long secondId = store.OfficialHolidays.Add(second, "نوروز");
        long sizdahId = store.OfficialHolidays.Add(sizdah, "سیزده‌به‌در");

        store.OfficialHolidays.DeleteMany([nowruzId, sizdahId]);
        OfficialHolidayRecord remaining = Assert.Single(store.OfficialHolidays.List(nowruz, sizdah));
        Assert.Equal(secondId, remaining.Id);

        ArgumentException gone = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.DeleteMany([nowruzId, secondId]));
        Assert.Equal("این تعطیل رسمی دیگر وجود ندارد.", gone.Message);
        Assert.Equal(secondId, Assert.Single(store.OfficialHolidays.List(nowruz, sizdah)).Id);

        ArgumentException empty = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.DeleteMany([]));
        Assert.Equal("یک تعطیل رسمی را انتخاب کنید.", empty.Message);
    }

    [Fact]
    public void DeleteRemovesOnlyThatRow()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly nowruz = Dates.ParseShamsiDate("1405/01/01");
        DateOnly sizdah = Dates.ParseShamsiDate("1405/01/13");
        long nowruzId = store.OfficialHolidays.Add(nowruz, "نوروز");
        long sizdahId = store.OfficialHolidays.Add(sizdah, "سیزده‌به‌در");

        store.OfficialHolidays.Delete(sizdahId);

        OfficialHolidayRecord remaining = Assert.Single(
            store.OfficialHolidays.List(nowruz, sizdah));
        Assert.Equal(nowruzId, remaining.Id);

        ArgumentException gone = Assert.Throws<ArgumentException>(() => store.OfficialHolidays.Delete(sizdahId));
        Assert.Equal("این تعطیل رسمی دیگر وجود ندارد.", gone.Message);

        ArgumentException missing = Assert.Throws<ArgumentException>(() => store.OfficialHolidays.Delete(0));
        Assert.Equal("یک تعطیل رسمی را انتخاب کنید.", missing.Message);
    }

    [Fact]
    public void ListRejectsAReversedRange()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly start = Dates.ParseShamsiDate("1405/01/01");
        DateOnly end = Dates.ParseShamsiDate("1405/01/13");

        ArgumentException range = Assert.Throws<ArgumentException>(
            () => store.OfficialHolidays.List(end, start));
        Assert.Contains("تاریخ پایان", range.Message, StringComparison.Ordinal);
    }
}
