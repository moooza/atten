using Atten.Core;
using Xunit;

namespace Atten.Data.Tests;

public class RepositoryTests : IDisposable
{
    public RepositoryTests()
    {
        StorageClock.Reset();
    }

    public void Dispose()
    {
        StorageClock.Reset();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ListPersonnelReturnsSavedRows()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        using (StorageClock.Freeze("2026-10-07T16:30:00"))
        {
            store.Personnel.Add("علی", "رضایی", 4.5, remoteId: "dev-1", mobile: "09120000000");
        }

        Assert.Equal(
            [
                new PersonnelRecord(
                    1,
                    "dev-1",
                    "علی",
                    "رضایی",
                    4.5,
                    "09120000000",
                    null,
                    null,
                    "2026-10-07T16:30:00",
                    "2026-10-07T16:30:00",
                    DefaultUser.Id,
                    DefaultUser.Id),
            ],
            store.Personnel.List());
    }

    [Fact]
    public void PersonnelStoresCooperationDatesAsGregorian()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly started = Dates.ParseShamsiDate("1405/07/01");
        DateOnly ended = Dates.ParseShamsiDate("1405/07/15");

        long personId = store.Personnel.Add("علی", "رضایی", 8, cooperationStart: started, cooperationEnd: ended);
        PersonnelRecord person = store.Personnel.List()[0];
        Assert.Equal("2026-09-23", person.CooperationStart);
        Assert.Equal("2026-10-07", person.CooperationEnd);

        store.Personnel.Update(personId, "علی", "رضایی", 8, cooperationStart: started);
        Assert.Null(store.Personnel.List()[0].CooperationEnd);

        ArgumentException missingStart = Assert.Throws<ArgumentException>(
            () => store.Personnel.Add("مریم", "احمدی", 8, cooperationEnd: ended));
        Assert.Equal("تاریخ شروع همکاری را وارد کنید.", missingStart.Message);

        ArgumentException reversed = Assert.Throws<ArgumentException>(
            () => store.Personnel.Add("مریم", "احمدی", 8, cooperationStart: ended, cooperationEnd: started));
        Assert.Contains("تاریخ پایان همکاری باید بعد از تاریخ شروع", reversed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PersonnelAndClockNamesAreStoredWithPersianLettersAndEnglishDigits()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();

        store.Personnel.Add(
            "عل\u064a",
            "\u0643اظم\u0649",
            8,
            remoteId: "\u06f1\u06f2\u0663",
            mobile: "۰۹۱۲۰۰۰۰۰۰۰");
        store.ClockEvents.Add("\u06f1\u06f2\u0663", "عل\u064a \u0643اظم\u064a", new DateOnly(2026, 10, 7), "۰۸:۳۰:۰۰");

        PersonnelRecord person = store.Personnel.List()[0];
        Assert.Equal("عل\u06cc", person.FirstName);
        Assert.Equal("\u06a9اظم\u06cc", person.LastName);
        Assert.Equal("123", person.RemoteId);
        Assert.Equal("09120000000", person.Mobile);
        ClockEventRecord ev = store.ClockEvents.List()[0];
        Assert.Equal("123", ev.RemoteId);
        Assert.Equal("عل\u06cc \u06a9اظم\u06cc", ev.Name);
        Assert.Equal("08:30:00", ev.Time);
    }

    [Fact]
    public void AddPersonnelRejectsBlankNameAndDuplicateRemoteId()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        store.Personnel.Add("علی", "رضایی", 8, remoteId: "dev-1");

        ArgumentException blank = Assert.Throws<ArgumentException>(
            () => store.Personnel.Add("  ", "رضایی", 8));
        Assert.Equal("نام را وارد کنید.", blank.Message);

        ArgumentException duplicate = Assert.Throws<ArgumentException>(
            () => store.Personnel.Add("مریم", "احمدی", 8, remoteId: "dev-1"));
        Assert.Equal("این کد پرسنلی قبلاً ثبت شده است.", duplicate.Message);
        Assert.Single(store.Personnel.List());
    }

    [Fact]
    public void UpdatePersonnelChangesTheSameRowAndWritesTheActor()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        long first;
        using (StorageClock.Freeze("2026-10-07T16:30:00"))
        {
            first = store.Personnel.Add("علی", "رضایی", 4.5, remoteId: "dev-1", mobile: "09120000000");
            store.Personnel.Add("مریم", "احمدی", 8, remoteId: "dev-2");
        }

        using (StorageClock.Freeze("2026-10-07T18:05:00"))
        {
            store.Personnel.Update(first, "علی", "کاظمی", 8, remoteId: "dev-1", mobile: "");
        }

        IReadOnlyList<PersonnelRecord> rows = store.Personnel.List();
        Assert.Equal(
            new PersonnelRecord(
                first,
                "dev-1",
                "علی",
                "کاظمی",
                8,
                null,
                null,
                null,
                "2026-10-07T16:30:00",
                "2026-10-07T18:05:00",
                DefaultUser.Id,
                DefaultUser.Id),
            rows[0]);
        Assert.Equal("dev-2", rows[1].RemoteId);
        Assert.Equal("2026-10-07T16:30:00", rows[1].CreatedAt);
        Assert.Equal("2026-10-07T16:30:00", rows[1].UpdatedAt);

        ArgumentException duplicate = Assert.Throws<ArgumentException>(
            () => store.Personnel.Update(first, "علی", "کاظمی", 8, remoteId: "dev-2"));
        Assert.Equal("این کد پرسنلی قبلاً ثبت شده است.", duplicate.Message);
    }

    [Fact]
    public void AddClockEventStoresAPunchForAKnownOrUnknownRemoteId()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        using (StorageClock.Freeze("2026-10-07T16:30:00"))
        {
            store.Personnel.Add("علی", "رضایی", 8, remoteId: "dev-1");
            long newId = store.ClockEvents.Add("dev-1", "علی رضایی", new DateOnly(2026, 10, 7), "8:30");
            Assert.Equal(1, newId);
        }

        Assert.Equal(
            [
                new ClockEventRecord(
                    1,
                    "dev-1",
                    "علی رضایی",
                    "2026-10-07",
                    "08:30:00",
                    "2026-10-07T16:30:00",
                    "2026-10-07T16:30:00",
                    DefaultUser.Id,
                    DefaultUser.Id),
            ],
            store.ClockEvents.List());

        long missing = store.ClockEvents.Add("missing", "کسی", new DateOnly(2026, 10, 7), "08:30:00");
        Assert.Equal(2, missing);
        Assert.Equal("missing", store.ClockEvents.List()[1].RemoteId);

        ArgumentException blank = Assert.Throws<ArgumentException>(
            () => store.ClockEvents.Add("dev-1", "  ", new DateOnly(2026, 10, 7), "08:30:00"));
        Assert.Equal("نام را وارد کنید.", blank.Message);

        ArgumentException clock = Assert.Throws<ArgumentException>(
            () => store.ClockEvents.Add("dev-1", "علی", new DateOnly(2026, 10, 7), "25:00"));
        Assert.Equal("ساعت معتبر نیست.", clock.Message);
        Assert.Equal(2, store.ClockEvents.List().Count);
    }

    [Fact]
    public void ListDailyPunchesGroupsOnePersonInsideTheRange()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        store.Personnel.Add("علی", "رضایی", 8, remoteId: "dev-1");
        store.Personnel.Add("مریم", "احمدی", 8, remoteId: "dev-2");
        store.ClockEvents.Add("dev-1", "علی", new DateOnly(2026, 10, 6), "07:00:00");
        store.ClockEvents.Add("dev-1", "علی", new DateOnly(2026, 10, 7), "12:00:00");
        store.ClockEvents.Add("dev-1", "علی", new DateOnly(2026, 10, 7), "08:00:00");
        store.ClockEvents.Add("dev-1", "علی", new DateOnly(2026, 10, 8), "09:15:00");
        store.ClockEvents.Add("dev-2", "مریم", new DateOnly(2026, 10, 7), "10:00:00");

        IReadOnlyList<DeviceDay> days = store.ClockEvents.ListDaily(
            "dev-1",
            new DateOnly(2026, 10, 7),
            new DateOnly(2026, 10, 8));
        Assert.Equal(["2026-10-07", "2026-10-08"], days.Select(day => day.Date));
        Assert.Equal(["08:00:00", "12:00:00"], days[0].Times);
        Assert.Equal(["09:15:00"], days[1].Times);
        Assert.Empty(store.ClockEvents.ListDaily("dev-1", new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 9)));

        ArgumentException reversed = Assert.Throws<ArgumentException>(
            () => store.ClockEvents.ListDaily("dev-1", new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 7)));
        Assert.Equal("تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.", reversed.Message);
    }

    [Fact]
    public void ImportStoresEveryRowWithoutAPersonnelRecordAndSkipsDuplicates()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        const string log =
            "1002\tKarimi\t2026-09-23 20:11:58\t1\t0\n" +
            "\n" +
            "1008\tjamalian\t2026-10-06 18:03:50\t1\t1\n" +
            "1002\tKarimi\t2026-09-23 20:11:58\t1\t0\n";

        ClockEventImport first;
        using (StorageClock.Freeze("2026-10-07T16:30:00"))
        {
            first = store.ClockEvents.Import(Attlog.Parse(log));
        }

        ClockEventImport second = store.ClockEvents.Import(Attlog.Parse(log));
        Assert.Equal(2, first.Added);
        Assert.Equal(1, first.Skipped);
        Assert.Equal(0, second.Added);
        Assert.Equal(3, second.Skipped);
        Assert.Equal(
            [
                new ClockEventRecord(
                    1,
                    "1002",
                    "Karimi",
                    "2026-09-23",
                    "20:11:58",
                    "2026-10-07T16:30:00",
                    "2026-10-07T16:30:00",
                    DefaultUser.Id,
                    DefaultUser.Id),
                new ClockEventRecord(
                    2,
                    "1008",
                    "jamalian",
                    "2026-10-06",
                    "18:03:50",
                    "2026-10-07T16:30:00",
                    "2026-10-07T16:30:00",
                    DefaultUser.Id,
                    DefaultUser.Id),
            ],
            store.ClockEvents.List());
    }

    [Fact]
    public void ImportRejectsABadClockWithoutWriting()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => store.ClockEvents.Import(Attlog.Parse("1002\tKarimi\t2026-09-23 25:00:00\t1\t0\n")));
        Assert.Equal("سطر 1: ساعت معتبر نیست.", error.Message);
        Assert.Empty(store.ClockEvents.List());
    }

    [Fact]
    public void WorkSheetStoresEditsWithoutChangingClockEvents()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        using (StorageClock.Freeze("2026-10-07T16:30:00"))
        {
            store.Personnel.Add("علی", "رضایی", 8, remoteId: "dev-1");
            store.ClockEvents.Add("dev-1", "علی", new DateOnly(2026, 10, 8), "09:00:00");
        }

        IReadOnlyList<ClockEventRecord> before = store.ClockEvents.List();
        store.Calculation.SavePunch("dev-1", new DateOnly(2026, 10, 8), 1, "17:30");
        store.Calculation.SavePunch("dev-1", new DateOnly(2026, 10, 8), 0, null);
        store.Calculation.SaveBalances(
            "dev-1",
            [
                (new DateOnly(2026, 10, 8), -480, false),
                (new DateOnly(2026, 10, 9), 30, true),
            ]);

        Assert.Equal(before, store.ClockEvents.List());
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, string?>> punches =
            store.Calculation.ListPunches("dev-1", new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9));
        Assert.Equal(new Dictionary<int, string?> { [0] = null, [1] = "17:30:00" }, punches["2026-10-08"]);
        Assert.Equal(
            [
                new WorkBalance("dev-1", "2026-10-08", -480, false),
                new WorkBalance("dev-1", "2026-10-09", 30, true),
            ],
            store.Calculation.ListBalances("dev-1", new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void ClearWorkPunchesDropsOneDayAndLeavesClockEvents()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        store.ClockEvents.Add("dev-1", "علی", new DateOnly(2026, 10, 8), "09:00:00");
        IReadOnlyList<ClockEventRecord> before = store.ClockEvents.List();
        store.Calculation.SavePunch("dev-1", new DateOnly(2026, 10, 8), 1, "17:30");
        store.Calculation.SavePunch("dev-1", new DateOnly(2026, 10, 9), 0, "08:00");
        store.Calculation.SavePunch("dev-2", new DateOnly(2026, 10, 8), 0, "10:00");

        store.Calculation.ClearPunches("dev-1", new DateOnly(2026, 10, 8));

        Assert.Equal(before, store.ClockEvents.List());
        Assert.Equal(
            new Dictionary<int, string?> { [0] = "08:00:00" },
            store.Calculation.ListPunches("dev-1", new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9))["2026-10-09"]);
        Assert.False(
            store.Calculation.ListPunches("dev-1", new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9))
                .ContainsKey("2026-10-08"));
        Assert.Equal(
            new Dictionary<int, string?> { [0] = "10:00:00" },
            store.Calculation.ListPunches("dev-2", new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 8))["2026-10-08"]);
    }

    [Fact]
    public void SaveHolidayUpdatesOnlyTheFlag()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        store.Calculation.SaveBalances("dev-1", [(new DateOnly(2026, 10, 8), -120, false)]);
        store.Calculation.SaveHoliday("dev-1", new DateOnly(2026, 10, 8), true);
        store.Calculation.SaveHoliday("dev-1", new DateOnly(2026, 10, 9), true);

        Assert.Equal(
            [
                new WorkBalance("dev-1", "2026-10-08", -120, true),
                new WorkBalance("dev-1", "2026-10-09", 0, true),
            ],
            store.Calculation.ListBalances("dev-1", new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void LeaveStoresMinutesAndKeepsEachShamsiYearSeparate()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly current = Dates.ParseShamsiDate("1405/06/15");
        DateOnly previous = Dates.ParseShamsiDate("1404/06/15");
        int amount = Leave.ComposeLeaveMinutes(1, 2, 15);
        long personId;
        long leaveId;
        using (StorageClock.Freeze("2026-10-07T16:30:00"))
        {
            personId = store.Personnel.Add(
                "علی",
                "رضایی",
                8,
                remoteId: "dev-1",
                cooperationStart: Dates.ParseShamsiDate("1404/01/01"));
            leaveId = store.Leaves.Add(personId, current, current, amount);
            store.Leaves.Add(personId, previous, previous, 30);
        }

        IReadOnlyList<LeaveRecord> rows = store.Leaves.List(personId);
        Assert.Equal(1, leaveId);
        Assert.Equal([2L, 1L], rows.Select(row => row.Id));
        Assert.Equal(amount, rows[1].Minutes);
        Assert.Equal(Dates.StorageDate(current), rows[1].StartDate);
        Assert.Equal("2026-10-07T16:30:00", rows[1].CreatedAt);
        Assert.Equal(DefaultUser.Id, rows[1].CreatedBy);
        Assert.Equal([30, amount], rows.Select(row => row.Minutes));

        var (yearStart, yearEnd) = Leave.ShamsiYearSpan(1405);
        Assert.Equal(amount, store.Leaves.SumMinutes(personId, yearStart, yearEnd));
        Assert.Equal([leaveId], store.Leaves.List(personId, current, current).Select(row => row.Id));
        Assert.Empty(
            store.Leaves.List(personId, Dates.ParseShamsiDate("1405/07/01"), Dates.ParseShamsiDate("1405/07/10")));

        ArgumentException over = Assert.Throws<ArgumentException>(
            () => store.Leaves.Add(personId, current, current, Leave.YearlyLeaveMinutes + Leave.CarryLeaveMinutes));
        Assert.Contains("بیشتر است", over.Message, StringComparison.Ordinal);

        ArgumentException range = Assert.Throws<ArgumentException>(
            () => store.Leaves.Add(personId, current, previous, 15));
        Assert.Contains("تاریخ پایان", range.Message, StringComparison.Ordinal);

        ArgumentException missing = Assert.Throws<ArgumentException>(
            () => store.Leaves.Add(99, current, current, 15));
        Assert.Contains("وجود ندارد", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SavingLeaveRejectsMoreThanTheSettledRemainder()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();
        DateOnly day = Dates.ParseShamsiDate("1405/06/15");
        long missing = store.Personnel.Add("علی", "رضایی", 8, remoteId: "dev-1");
        ArgumentException none = Assert.Throws<ArgumentException>(() => store.Leaves.Add(missing, day, day, 1));
        Assert.Contains("باقی‌مانده: 0 روز و 0 ساعت و 0 دقیقه", none.Message, StringComparison.Ordinal);

        DateOnly oneDay = Dates.ParseShamsiDate("1405/01/01");
        long partial = store.Personnel.Add(
            "مریم",
            "احمدی",
            8,
            remoteId: "dev-2",
            cooperationStart: oneDay,
            cooperationEnd: oneDay);
        store.Leaves.Add(partial, oneDay, oneDay, 35);
        ArgumentException used = Assert.Throws<ArgumentException>(() => store.Leaves.Add(partial, oneDay, oneDay, 1));
        Assert.Contains("باقی‌مانده: 0 روز و 0 ساعت و 0 دقیقه", used.Message, StringComparison.Ordinal);

        long carried = store.Personnel.Add(
            "سارا",
            "کریمی",
            8,
            remoteId: "dev-3",
            cooperationStart: Dates.ParseShamsiDate("1404/01/01"));
        int allowance = Leave.YearlyLeaveMinutes + Leave.CarryLeaveMinutes;
        long leaveId = store.Leaves.Add(carried, day, day, allowance);
        Assert.Throws<ArgumentException>(() => store.Leaves.Add(carried, day, day, 1));
        store.Leaves.Update(leaveId, carried, day, day, allowance);
        Assert.Throws<ArgumentException>(() => store.Leaves.Update(leaveId, carried, day, day, allowance + 1));
        DateOnly before = Dates.ParseShamsiDate("1403/06/15");
        ArgumentException early = Assert.Throws<ArgumentException>(
            () => store.Leaves.Update(leaveId, carried, before, before, 1));
        Assert.Contains("باقی‌مانده: 0 روز و 0 ساعت و 0 دقیقه", early.Message, StringComparison.Ordinal);
        Assert.Equal(allowance, store.Leaves.List(carried)[0].Minutes);

        long gapped = store.Personnel.Add(
            "نادر",
            "موسوی",
            8,
            remoteId: "dev-4",
            cooperationStart: Dates.ParseShamsiDate("1402/01/01"),
            cooperationEnd: Dates.ParseShamsiDate("1402/12/29"));
        ArgumentException gap = Assert.Throws<ArgumentException>(() => store.Leaves.Add(gapped, day, day, 1));
        Assert.Contains("باقی‌مانده: 0 روز و 0 ساعت و 0 دقیقه", gap.Message, StringComparison.Ordinal);
    }

}
