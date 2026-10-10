using Xunit;

namespace Atten.Core.Tests;

public class SheetTests
{
    private static readonly Dictionary<string, IReadOnlyDictionary<int, string?>> NoOverrides = [];

    [Fact]
    public void BalanceTextUsesMinutesAfterTheDot()
    {
        Assert.Equal("-2.20", Sheet.FormatBalance(-140));
        Assert.Equal("-3.58", Sheet.FormatBalance(-238));
        Assert.Equal("+3.00", Sheet.FormatBalance(180));
        Assert.Equal("+0.59", Sheet.FormatBalance(59));
        Assert.Equal("+1.00", Sheet.FormatBalance(60));
        Assert.Equal("+1.59", Sheet.FormatBalance(119));
        Assert.Equal("+2.00", Sheet.FormatBalance(120));
        Assert.Equal("0.00", Sheet.FormatBalance(0));
        Assert.Equal(480, Sheet.RequiredMinutes(8));
        Assert.Equal(270, Sheet.RequiredMinutes(4.5));
    }

    [Fact]
    public void SheetIncludesEveryDayAndLeavesABlankExitForAnOpenEntry()
    {
        DeviceDay[] device =
        [
            new("2026-10-07", ["08:00:00", "12:00:00", "13:00:00", "17:00:00", "18:00:00", "21:00:00"]),
            new("2026-10-08", ["09:00:00"]),
        ];
        IReadOnlyList<SheetRow> rows = Sheet.BuildSheet(
            new DateOnly(2026, 10, 7),
            new DateOnly(2026, 10, 9),
            device,
            NoOverrides,
            8);

        Assert.Equal(["2026-10-07", "2026-10-08", "2026-10-09"], rows.Select(row => row.Date));
        Assert.Equal(
            ["08:00:00", "12:00:00", "13:00:00", "17:00:00", "18:00:00", "21:00:00"],
            rows[0].Slots);
        Assert.False(rows[0].Incomplete);
        Assert.Equal(180, rows[0].BalanceMinutes);
        Assert.Equal("+3.00", Sheet.FormatBalance(rows[0].BalanceMinutes));
        Assert.False(rows[0].Holiday);
        Assert.Equal(new string?[] { "09:00:00", null }, rows[1].Slots);
        Assert.True(rows[1].Incomplete);
        Assert.False(rows[1].Holiday);
        Assert.Equal(-480, rows[1].BalanceMinutes);
        Assert.Equal(new string?[] { null, null }, rows[2].Slots);
        Assert.True(rows[2].Holiday);
        Assert.False(rows[2].Incomplete);
        Assert.Equal(0, rows[2].BalanceMinutes);
    }

    [Fact]
    public void HolidayPresenceIsOvertimeAndAClearedFridayUsesDailyHours()
    {
        IReadOnlyList<SheetRow> present = Sheet.BuildSheet(
            new DateOnly(2026, 10, 9),
            new DateOnly(2026, 10, 9),
            [new DeviceDay("2026-10-09", ["09:00:00", "13:00:00"])],
            NoOverrides,
            8);
        Assert.True(present[0].Holiday);
        Assert.Equal(240, present[0].BalanceMinutes);
        Assert.Equal("+4.00", Sheet.FormatBalance(present[0].BalanceMinutes));

        IReadOnlyList<SheetRow> workingFriday = Sheet.BuildSheet(
            new DateOnly(2026, 10, 9),
            new DateOnly(2026, 10, 9),
            [],
            NoOverrides,
            8,
            new Dictionary<string, bool> { ["2026-10-09"] = false });
        Assert.False(workingFriday[0].Holiday);
        Assert.Equal(-480, workingFriday[0].BalanceMinutes);
        Assert.True(workingFriday[0].Incomplete);

        IReadOnlyList<SheetRow> ticked = Sheet.BuildSheet(
            new DateOnly(2026, 10, 7),
            new DateOnly(2026, 10, 7),
            [new DeviceDay("2026-10-07", ["08:00:00", "12:00:00", "13:00:00", "17:00:00", "18:00:00", "21:00:00"])],
            NoOverrides,
            8,
            new Dictionary<string, bool> { ["2026-10-07"] = true });
        Assert.True(ticked[0].Holiday);
        Assert.Equal(660, ticked[0].BalanceMinutes);
        Assert.Equal("+11.00", Sheet.FormatBalance(ticked[0].BalanceMinutes));
    }

    [Fact]
    public void ManualExitAndHalfHourDayChangeTheBalance()
    {
        DeviceDay[] device = [new("2026-10-08", ["09:00:00"])];
        var overrides = new Dictionary<string, IReadOnlyDictionary<int, string?>>
        {
            ["2026-10-08"] = new Dictionary<int, string?> { [1] = "17:30:00" },
        };
        IReadOnlyList<SheetRow> rows = Sheet.BuildSheet(
            new DateOnly(2026, 10, 8),
            new DateOnly(2026, 10, 8),
            device,
            overrides,
            8);
        Assert.Equal(["09:00:00", "17:30:00"], rows[0].Slots);
        Assert.False(rows[0].Incomplete);
        Assert.Equal(30, rows[0].BalanceMinutes);
        Assert.Equal("+0.30", Sheet.FormatBalance(rows[0].BalanceMinutes));

        IReadOnlyList<SheetRow> shortDay = Sheet.BuildSheet(
            new DateOnly(2026, 10, 8),
            new DateOnly(2026, 10, 8),
            [new DeviceDay("2026-10-08", ["08:00:00", "12:00:00"])],
            NoOverrides,
            4.5);
        Assert.Equal(-30, shortDay[0].BalanceMinutes);
        Assert.Equal("-0.30", Sheet.FormatBalance(shortDay[0].BalanceMinutes));
    }

    [Fact]
    public void BlankOverrideHidesADevicePunch()
    {
        DeviceDay[] device = [new("2026-10-08", ["09:00:00", "17:00:00"])];
        IReadOnlyList<SheetRow> rows = Sheet.BuildSheet(
            new DateOnly(2026, 10, 8),
            new DateOnly(2026, 10, 8),
            device,
            new Dictionary<string, IReadOnlyDictionary<int, string?>>
            {
                ["2026-10-08"] = new Dictionary<int, string?> { [1] = null },
            },
            8);
        Assert.Equal(new string?[] { "09:00:00", null }, rows[0].Slots);
        Assert.True(rows[0].Incomplete);
    }

    [Fact]
    public void SameDayLeaveCancelsTheShortfallAndDoesNotBecomeOvertime()
    {
        DateOnly day = new(2026, 9, 30);
        DeviceDay[] punches = [new("2026-09-30", ["09:43:10", "13:50:33"])];
        double dailyHours = 7 + 20.0 / 60;
        IReadOnlyList<SheetRow> bare = Sheet.BuildSheet(day, day, punches, NoOverrides, dailyHours);
        Assert.Equal(-193, bare[0].BalanceMinutes);
        Assert.Equal("-3.13", Sheet.FormatBalance(bare[0].BalanceMinutes));

        IReadOnlyList<SheetRow> covered = Sheet.BuildSheet(
            day,
            day,
            punches,
            NoOverrides,
            dailyHours,
            leaves: [new LeaveSpan(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), 193)]);
        Assert.Equal(0, covered[0].BalanceMinutes);
        Assert.False(covered[0].Incomplete);
        Assert.True(covered[0].OnLeave);
        Assert.Equal(193, covered[0].LeaveMinutes);
        Assert.Equal("0.00", Sheet.FormatBalance(covered[0].BalanceMinutes));
        Assert.False(bare[0].OnLeave);
        Assert.Equal(0, bare[0].LeaveMinutes);

        IReadOnlyList<SheetRow> partial = Sheet.BuildSheet(
            day,
            day,
            punches,
            NoOverrides,
            dailyHours,
            leaves: [new LeaveSpan(day, day, 60)]);
        Assert.Equal(-133, partial[0].BalanceMinutes);

        IReadOnlyList<SheetRow> extra = Sheet.BuildSheet(
            day,
            day,
            punches,
            NoOverrides,
            dailyHours,
            leaves: [new LeaveSpan(day, day, 300)]);
        Assert.Equal(0, extra[0].BalanceMinutes);

        IReadOnlyList<SheetRow> present = Sheet.BuildSheet(
            day,
            day,
            [new DeviceDay("2026-09-30", ["09:00:00", "17:00:00"])],
            NoOverrides,
            dailyHours,
            leaves: [new LeaveSpan(day, day, 193)]);
        Assert.Equal(40, present[0].BalanceMinutes);
        Assert.Equal("+0.40", Sheet.FormatBalance(present[0].BalanceMinutes));
    }

    [Fact]
    public void MultiDayLeaveCoversEachAbsentWorkingDay()
    {
        double dailyHours = 7 + 20.0 / 60;
        int dayMinutes = Sheet.RequiredMinutes(dailyHours);
        IReadOnlyList<SheetRow> rows = Sheet.BuildSheet(
            new DateOnly(2026, 10, 5),
            new DateOnly(2026, 10, 8),
            [],
            NoOverrides,
            dailyHours,
            leaves: [new LeaveSpan(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7), 3 * dayMinutes)]);
        Assert.Equal([0, 0, 0, -dayMinutes], rows.Select(row => row.BalanceMinutes));
        Assert.Equal([dayMinutes, dayMinutes, dayMinutes, 0], rows.Select(row => row.LeaveMinutes));
        Assert.Equal([true, true, true, false], rows.Select(row => row.OnLeave));
        Assert.Equal([false, false, false, true], rows.Select(row => row.Incomplete));
        Assert.False(rows[3].Holiday);

        IReadOnlyList<SheetRow> window = Sheet.BuildSheet(
            new DateOnly(2026, 10, 7),
            new DateOnly(2026, 10, 7),
            [],
            NoOverrides,
            dailyHours,
            leaves: [new LeaveSpan(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7), 300)]);
        Assert.Equal(100 - dayMinutes, window[0].BalanceMinutes);

        IReadOnlyList<SheetRow> acrossFriday = Sheet.BuildSheet(
            new DateOnly(2026, 10, 8),
            new DateOnly(2026, 10, 10),
            [],
            NoOverrides,
            dailyHours,
            leaves: [new LeaveSpan(new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 10), 2 * dayMinutes)]);
        Assert.Equal([false, true, false], acrossFriday.Select(row => row.Holiday));
        Assert.Equal([true, true, true], acrossFriday.Select(row => row.OnLeave));
        Assert.Equal([dayMinutes, 0, dayMinutes], acrossFriday.Select(row => row.LeaveMinutes));
        Assert.Equal([0, 0, 0], acrossFriday.Select(row => row.BalanceMinutes));
        Assert.Equal([false, false, false], acrossFriday.Select(row => row.Incomplete));
    }

    [Fact]
    public void ParseClockAcceptsPersianDigitsAndRejectsBadTimes()
    {
        Assert.Equal("17:30:00", Sheet.ParseClock(" ۱۷:۳۰ "));
        Assert.Equal("08:05:09", Sheet.ParseClock("۰۸:۰۵:۰۹"));
        Assert.Null(Sheet.ParseClock("  "));
        FormatException dotted = Assert.Throws<FormatException>(() => Sheet.ParseClock("3.60"));
        Assert.Contains("ساعت معتبر نیست", dotted.Message);
        FormatException late = Assert.Throws<FormatException>(() => Sheet.ParseClock("24:00"));
        Assert.Contains("ساعت معتبر نیست", late.Message);
    }
}
