using Xunit;

namespace Atten.Core.Tests;

public class LeaveTests
{
    [Fact]
    public void AFullMonthIsTwoAndAHalfDaysAndAPartialMonthRounds()
    {
        Assert.Equal(Leave.LeaveDayMinutes * 5 / 2, Leave.MonthlyLeaveMinutes);
        Assert.Equal(Leave.MonthlyLeaveMinutes, Leave.MonthLeaveMinutes(30, 30));
        Assert.Equal(Leave.MonthlyLeaveMinutes, Leave.MonthLeaveMinutes(31, 31));
        Assert.Equal(Leave.MonthlyLeaveMinutes, Leave.MonthLeaveMinutes(29, 29));
        Assert.Equal(37, Leave.MonthLeaveMinutes(1, 30));
        Assert.Equal(35, Leave.MonthLeaveMinutes(1, 31));
        Assert.Equal(38, Leave.MonthLeaveMinutes(1, 29));
        Assert.Equal(587, Leave.MonthLeaveMinutes(16, 30));
        Assert.Equal(0, Leave.MonthLeaveMinutes(0, 30));
        Assert.Equal(0, Leave.MonthLeaveMinutes(5, 0));
    }

    [Fact]
    public void AFullCooperationYearIsThirtyDaysInLeapAndPlainYears()
    {
        Assert.Equal(30 * Leave.LeaveDayMinutes, Leave.YearlyLeaveMinutes);
        Assert.Equal(12 * Leave.MonthlyLeaveMinutes, Leave.YearlyLeaveMinutes);
        int leap = Leave.EarnedLeaveMinutes(1403, Dates.ParseShamsiDate("1403/01/01"), Dates.ParseShamsiDate("1403/12/30"));
        int plain = Leave.EarnedLeaveMinutes(1404, Dates.ParseShamsiDate("1404/01/01"), Dates.ParseShamsiDate("1404/12/29"));
        Assert.Equal(Leave.YearlyLeaveMinutes, leap);
        Assert.Equal(Leave.YearlyLeaveMinutes, plain);
    }

    [Fact]
    public void APartialMonthUsesTheCoveredCalendarDays()
    {
        DateOnly oneDay = Dates.ParseShamsiDate("1405/01/01");
        Assert.Equal(35, Leave.EarnedLeaveMinutes(1405, oneDay, oneDay));
        DateOnly esfand = Dates.ParseShamsiDate("1404/12/29");
        Assert.Equal(38, Leave.EarnedLeaveMinutes(1404, esfand, esfand));
        Assert.Equal(Leave.MonthlyLeaveMinutes, Leave.EarnedLeaveMinutes(1404, Dates.ParseShamsiDate("1404/12/01"), esfand));
    }

    [Fact]
    public void StartAndEndInsideOneYearIncludeBothEnds()
    {
        DateOnly start = Dates.ParseShamsiDate("1405/01/10");
        DateOnly end = Dates.ParseShamsiDate("1405/03/05");
        Assert.Equal(2058, Leave.EarnedLeaveMinutes(1405, start, end));
        Assert.Equal(0, Leave.EarnedLeaveMinutes(1404, start, end));
        Assert.Equal(0, Leave.EarnedLeaveMinutes(1406, start, end));
        Assert.Equal(0, Leave.EarnedLeaveMinutes(1405, Dates.ParseShamsiDate("1405/08/01"), Dates.ParseShamsiDate("1405/07/01")));
    }

    [Fact]
    public void CalendarDaysInsideAMonthIncludeFriday()
    {
        DateOnly start = Dates.ParseShamsiDate("1405/07/15");
        DateOnly end = Dates.ParseShamsiDate("1405/07/21");
        Assert.Equal(257, Leave.EarnedLeaveMinutes(1405, start, end));
    }

    [Fact]
    public void ASpanAcrossYearsClipsEachShamsiYear()
    {
        DateOnly start = Dates.ParseShamsiDate("1403/11/20");
        DateOnly end = Dates.ParseShamsiDate("1405/02/10");
        Assert.Equal(0, Leave.EarnedLeaveMinutes(1402, start, end));
        Assert.Equal(1503, Leave.EarnedLeaveMinutes(1403, start, end));
        Assert.Equal(Leave.YearlyLeaveMinutes, Leave.EarnedLeaveMinutes(1404, start, end));
        Assert.Equal(1455, Leave.EarnedLeaveMinutes(1405, start, end));
        Assert.Equal(0, Leave.EarnedLeaveMinutes(1406, start, end));
    }

    [Fact]
    public void AnOpenEndCountsThroughTheEndOfThatYear()
    {
        DateOnly started = Dates.ParseShamsiDate("1405/01/01");
        Assert.Equal(Leave.YearlyLeaveMinutes, Leave.EarnedLeaveMinutes(1405, started, null));
        Assert.Equal(Leave.YearlyLeaveMinutes, Leave.EarnedLeaveMinutes(1406, started));
        Assert.Equal(0, Leave.EarnedLeaveMinutes(1404, started, null));
        Assert.Equal(6087, Leave.EarnedLeaveMinutes(1405, Dates.ParseShamsiDate("1405/07/15"), Leave.OptionalDate("")));
        Assert.Equal(0, Leave.EarnedLeaveMinutes(1405, null, started));
        Assert.Equal(0, Leave.EarnedLeaveMinutes(1405, Leave.OptionalDate("  "), started));
    }

    [Fact]
    public void UnusedLeaveCarriesNineDays()
    {
        int full = Leave.YearlyLeaveMinutes;
        IReadOnlyList<LeaveYearSettlement> settled = Leave.SettleLeaveYears([(1404, full, 0)]);
        Assert.Equal(
            [
                new LeaveYearSettlement(
                    Year: 1404,
                    Earned: full,
                    CarryIn: 0,
                    Used: 0,
                    Usable: full,
                    Remaining: full,
                    CarryOut: Leave.CarryLeaveMinutes),
            ],
            settled);
    }

    [Fact]
    public void TheNextCooperationYearReceivesOnlyTheCarriedDays()
    {
        int full = Leave.YearlyLeaveMinutes;
        IReadOnlyList<LeaveYearSettlement> settled = Leave.SettleLeaveYears([(1405, full, 100), (1404, full, 0)]);
        LeaveYearSettlement first = settled[0];
        LeaveYearSettlement second = settled[1];
        Assert.Equal(1404, first.Year);
        Assert.Equal(Leave.CarryLeaveMinutes, first.CarryOut);
        Assert.Equal(Leave.CarryLeaveMinutes, second.CarryIn);
        Assert.Equal(full + Leave.CarryLeaveMinutes, second.Usable);
        Assert.Equal(full + Leave.CarryLeaveMinutes - 100, second.Remaining);
        Assert.Equal(Leave.CarryLeaveMinutes, second.CarryOut);
    }

    [Fact]
    public void RemainingWithinNineDaysCarriesAllOfIt()
    {
        int full = Leave.YearlyLeaveMinutes;
        LeaveYearSettlement row = Leave.SettleLeaveYears([(1404, full, full - 1000)])[0];
        Assert.Equal(1000, row.Remaining);
        Assert.Equal(1000, row.CarryOut);
        LeaveYearSettlement exact = Leave.SettleLeaveYears([(1404, full, full - Leave.CarryLeaveMinutes)])[0];
        Assert.Equal(Leave.CarryLeaveMinutes, exact.Remaining);
        Assert.Equal(Leave.CarryLeaveMinutes, exact.CarryOut);
    }

    [Fact]
    public void AMissingCooperationYearDropsTheCarry()
    {
        int full = Leave.YearlyLeaveMinutes;
        IReadOnlyList<LeaveYearSettlement> settled = Leave.SettleLeaveYears(
            [(1405, full, 500), (1402, full, 0), (1404, full, 0)]);
        Dictionary<int, LeaveYearSettlement> byYear = settled.ToDictionary(row => row.Year);
        Assert.Equal([1402, 1404, 1405], settled.Select(row => row.Year));
        Assert.Equal(Leave.CarryLeaveMinutes, byYear[1402].CarryOut);
        Assert.Equal(0, byYear[1404].CarryIn);
        Assert.Equal(full, byYear[1404].Usable);
        Assert.Equal(Leave.CarryLeaveMinutes, byYear[1404].CarryOut);
        Assert.Equal(Leave.CarryLeaveMinutes, byYear[1405].CarryIn);
        Assert.Equal(500, byYear[1405].Used);
    }

    [Fact]
    public void NonPositiveRemainingCarriesNothing()
    {
        int full = Leave.YearlyLeaveMinutes;
        IReadOnlyList<LeaveYearSettlement> settled = Leave.SettleLeaveYears([(1404, full, full + 10), (1405, 500, 0)]);
        LeaveYearSettlement spent = settled[0];
        LeaveYearSettlement following = settled[1];
        Assert.Equal(-10, spent.Remaining);
        Assert.Equal(0, spent.CarryOut);
        Assert.Equal(0, following.CarryIn);
        Assert.Equal(500, following.Usable);
        Assert.Equal(500, following.Remaining);
        Assert.Equal(500, following.CarryOut);
        LeaveYearSettlement exact = Leave.SettleLeaveYears([(1403, full, full)])[0];
        Assert.Equal(0, exact.Remaining);
        Assert.Equal(0, exact.CarryOut);
    }

    [Fact]
    public void RemainingIsZeroWithoutACooperationStart()
    {
        Assert.Equal(0, Leave.RemainingLeaveMinutes(1405, null, null, new Dictionary<int, int>()));
        Assert.Equal(-40, Leave.RemainingLeaveMinutes(1405, Leave.OptionalDate("  "), null, new Dictionary<int, int> { [1405] = 40 }));
    }

    [Fact]
    public void RemainingIsTheEarnedMinutesOfAPartialYear()
    {
        DateOnly day = Dates.ParseShamsiDate("1405/01/01");
        Assert.Equal(35, Leave.RemainingLeaveMinutes(1405, day, day, new Dictionary<int, int>()));
        Assert.Equal(25, Leave.RemainingLeaveMinutes(1405, day, day, new Dictionary<int, int> { [1405] = 10 }));
        Assert.Equal(0, Leave.RemainingLeaveMinutes(1404, day, day, new Dictionary<int, int>()));
    }

    [Fact]
    public void RemainingIncludesCarryFromThePreviousCooperationYear()
    {
        DateOnly start = Dates.ParseShamsiDate("1403/01/01");
        Assert.Equal(Leave.YearlyLeaveMinutes, Leave.RemainingLeaveMinutes(1403, start, null, new Dictionary<int, int>()));
        Assert.Equal(
            Leave.YearlyLeaveMinutes + Leave.CarryLeaveMinutes,
            Leave.RemainingLeaveMinutes(1404, start, null, new Dictionary<int, int>()));
        var used = new Dictionary<int, int>
        {
            [1403] = Leave.YearlyLeaveMinutes,
            [1404] = 100,
        };
        Assert.Equal(Leave.YearlyLeaveMinutes - 100, Leave.RemainingLeaveMinutes(1404, start, null, used));
    }

    [Fact]
    public void AGapInCooperationDropsCarryFromTheRemaining()
    {
        DateOnly start = Dates.ParseShamsiDate("1403/01/01");
        DateOnly end = Dates.ParseShamsiDate("1403/12/30");
        Assert.Equal(Leave.YearlyLeaveMinutes, Leave.RemainingLeaveMinutes(1403, start, end, new Dictionary<int, int>()));
        Assert.Equal(0, Leave.RemainingLeaveMinutes(1404, start, end, new Dictionary<int, int>()));
        Assert.Equal(-15, Leave.RemainingLeaveMinutes(1405, start, end, new Dictionary<int, int> { [1405] = 15 }));
    }

    [Fact]
    public void RepeatedRowsForOneYearAreAddedBeforeSettlement()
    {
        LeaveYearSettlement row = Leave.SettleLeaveYears([(1404, 400, 50), (1404, 600, 25)])[0];
        Assert.Equal(1000, row.Earned);
        Assert.Equal(75, row.Used);
        Assert.Equal(925, row.Remaining);
        Assert.Equal(925, row.CarryOut);
    }

    [Fact]
    public void EndingCooperationKeepsTheBalanceBeforeTransferAvailable()
    {
        DateOnly start = Dates.ParseShamsiDate("1404/01/01");
        DateOnly end = Dates.ParseShamsiDate("1404/12/29");
        int used = 11 * Leave.LeaveDayMinutes;
        Assert.Equal(
            19 * Leave.LeaveDayMinutes,
            Leave.RemainingLeaveMinutes(1404, start, end, new Dictionary<int, int> { [1404] = used }));
        Assert.Equal(
            19 * Leave.LeaveDayMinutes,
            Leave.RemainingLeaveMinutes(1404, start, null, new Dictionary<int, int> { [1404] = used }));
    }

    [Fact]
    public void TheCooperationClosingYearCarriesNothing()
    {
        int full = Leave.YearlyLeaveMinutes;
        int used = 11 * Leave.LeaveDayMinutes;
        LeaveYearSettlement openYear = Leave.SettleLeaveYears([(1404, full, used)])[0];
        Assert.Equal(19 * Leave.LeaveDayMinutes, openYear.Remaining);
        Assert.Equal(Leave.CarryLeaveMinutes, openYear.CarryOut);
        LeaveYearSettlement closed = Leave.SettleLeaveYears([(1404, full, used)], closingYear: 1404)[0];
        Assert.Equal(19 * Leave.LeaveDayMinutes, closed.Remaining);
        Assert.Equal(0, closed.CarryOut);
        IReadOnlyList<LeaveYearSettlement> settled = Leave.SettleLeaveYears(
            [(1404, full, used), (1405, full, 0)],
            closingYear: 1404);
        Assert.Equal(0, settled[0].CarryOut);
        Assert.Equal(0, settled[1].CarryIn);
        Assert.Equal(Leave.CarryLeaveMinutes, settled[1].CarryOut);
    }

    [Fact]
    public void CooperationYearChoicesAlwaysIncludesTheCurrentYear()
    {
        DateOnly today = Dates.ParseShamsiDate("1405/07/18");
        Assert.Equal([1405], Leave.CooperationYearChoices(null, null, today));
        Assert.Equal(
            [1405, 1404],
            Leave.CooperationYearChoices(Dates.ParseShamsiDate("1404/01/01"), null, today));
        Assert.Equal(
            [1405, 1403],
            Leave.CooperationYearChoices(
                Dates.ParseShamsiDate("1403/01/01"),
                Dates.ParseShamsiDate("1403/12/29"),
                today));
    }

    [Fact]
    public void SignedLeaveAmountKeepsALeadingMinus()
    {
        Assert.Equal("1 روز و 2 ساعت و 15 دقیقه", Leave.FormatLeaveAmount(Leave.ComposeLeaveMinutes(1, 2, 15)));
        Assert.Equal("-1 روز و 0 ساعت و 0 دقیقه", Leave.FormatSignedLeaveAmount(-Leave.LeaveDayMinutes));
        Assert.Equal("0 روز و 0 ساعت و 0 دقیقه", Leave.FormatSignedLeaveAmount(0));
    }

    [Fact]
    public void YearBalanceZerosWhenTheCooperationStartIsMissing()
    {
        var used = new Dictionary<int, int> { [1405] = 100 };
        LeaveYearBalance missing = Leave.YearBalance(1405, null, new Dictionary<int, LeaveYearSettlement>(), used);
        Assert.True(missing.MissingStart);
        Assert.Equal(0, missing.Used);
        Assert.Equal(0, missing.RemainingBeforeTransfer);

        IReadOnlyDictionary<int, LeaveYearSettlement> settled = Leave.SettleCooperationYears(
            Dates.ParseShamsiDate("1404/01/01"),
            null,
            new Dictionary<int, int> { [1404] = Leave.LeaveDayMinutes },
            Dates.ParseShamsiDate("1405/07/18"));
        LeaveYearBalance current = Leave.YearBalance(
            1405,
            Dates.ParseShamsiDate("1404/01/01"),
            settled,
            new Dictionary<int, int> { [1404] = Leave.LeaveDayMinutes });
        Assert.Equal(Leave.CarryLeaveMinutes, current.Carry);
        Assert.Equal(Leave.YearlyLeaveMinutes, current.Earned);
        Assert.Equal(Leave.CarryLeaveMinutes, current.Transfer);
    }

    [Fact]
    public void CalendarDaySpanIsEmptyWhenDatesMatchOrReverse()
    {
        DateOnly start = Dates.ParseShamsiDate("1405/01/01");
        DateOnly next = Dates.ParseShamsiDate("1405/01/03");
        Assert.Null(Leave.CalendarDaySpan(start, start));
        Assert.Null(Leave.CalendarDaySpan(next, start));
        Assert.Equal(3, Leave.CalendarDaySpan(start, next));
    }

    [Fact]
    public void WorkingDayCountSkipsFridayAndOfficialHolidays()
    {
        DateOnly wednesday = Dates.ParseShamsiDate("1405/07/15");
        DateOnly thursday = Dates.ParseShamsiDate("1405/07/16");
        DateOnly saturday = Dates.ParseShamsiDate("1405/07/18");
        static bool fridayOnly(DateOnly day) => OfficialHoliday.IsHoliday(day);

        Assert.Equal(3, Leave.WorkingDayCount(wednesday, saturday, fridayOnly));
        Assert.Null(Leave.WorkingDayCount(wednesday, wednesday, fridayOnly));
        Assert.Null(Leave.WorkingDayCount(saturday, wednesday, fridayOnly));

        IReadOnlySet<string> official = OfficialHoliday.Catalog([thursday]);
        Assert.Equal(2, Leave.WorkingDayCount(wednesday, saturday, day => OfficialHoliday.IsHoliday(day, official)));
        Assert.Equal(0, Leave.WorkingDayCount(thursday, Dates.ParseShamsiDate("1405/07/17"), day => OfficialHoliday.IsHoliday(day, official)));
    }

    [Fact]
    public void OverlapsWorkingDaysOnlyOnSharedWorkingDays()
    {
        DateOnly wednesday = Dates.ParseShamsiDate("1405/07/15");
        DateOnly thursday = Dates.ParseShamsiDate("1405/07/16");
        DateOnly friday = Dates.ParseShamsiDate("1405/07/17");
        DateOnly saturday = Dates.ParseShamsiDate("1405/07/18");
        static bool fridayOnly(DateOnly day) => OfficialHoliday.IsHoliday(day);

        Assert.True(Leave.OverlapsWorkingDays(wednesday, wednesday, wednesday, wednesday, fridayOnly));
        Assert.True(Leave.OverlapsWorkingDays(wednesday, saturday, thursday, thursday, fridayOnly));
        Assert.False(Leave.OverlapsWorkingDays(wednesday, friday, friday, saturday, fridayOnly));
        Assert.False(Leave.OverlapsWorkingDays(wednesday, wednesday, thursday, thursday, fridayOnly));
        Assert.False(Leave.OverlapsWorkingDays(wednesday, thursday, saturday, saturday, fridayOnly));

        IReadOnlySet<string> official = OfficialHoliday.Catalog([thursday]);
        bool withOfficial(DateOnly day) => OfficialHoliday.IsHoliday(day, official);
        Assert.False(Leave.OverlapsWorkingDays(wednesday, thursday, thursday, friday, withOfficial));
        Assert.True(Leave.OverlapsWorkingDays(wednesday, saturday, thursday, saturday, withOfficial));
    }

    [Fact]
    public void ACustomCarryCapReplacesTheNineDayDefault()
    {
        int full = Leave.YearlyLeaveMinutes;
        DateOnly start = Dates.ParseShamsiDate("1404/01/01");
        IReadOnlyList<LeaveYearSettlement> defaults = Leave.SettleLeaveYears([(1404, full, 0), (1405, full, 0)]);
        Assert.Equal(Leave.CarryLeaveMinutes, defaults[0].CarryOut);
        Assert.Equal(Leave.CarryLeaveMinutes, defaults[1].CarryIn);
        Assert.Equal(
            full + Leave.CarryLeaveMinutes,
            Leave.RemainingLeaveMinutes(1405, start, null, new Dictionary<int, int>()));

        var caps = new Dictionary<int, int> { [1404] = full };
        IReadOnlyList<LeaveYearSettlement> custom = Leave.SettleLeaveYears(
            [(1404, full, 0), (1405, full, 0)],
            carryCaps: caps);
        Assert.Equal(full, custom[0].CarryOut);
        Assert.Equal(full, custom[1].CarryIn);
        Assert.Equal(2 * full, custom[1].Usable);
        Assert.Equal(Leave.CarryLeaveMinutes, custom[1].CarryOut);
        Assert.Equal(2 * full, Leave.RemainingLeaveMinutes(1405, start, null, new Dictionary<int, int>(), caps));

        IReadOnlyDictionary<int, LeaveYearSettlement> settled = Leave.SettleCooperationYears(
            start,
            null,
            new Dictionary<int, int>(),
            Dates.ParseShamsiDate("1405/07/18"),
            caps);
        Assert.Equal(full, settled[1404].CarryOut);
        Assert.Equal(2 * full, settled[1405].Remaining);

        LeaveYearSettlement closed = Leave.SettleLeaveYears(
            [(1404, full, 0)],
            closingYear: 1404,
            carryCaps: caps)[0];
        Assert.Equal(full, closed.Remaining);
        Assert.Equal(0, closed.CarryOut);
    }

    [Fact]
    public void LeftoverActionsNeedAClosedYearOrTheClosingYear()
    {
        DateOnly today = Dates.ParseShamsiDate("1405/07/18");
        Assert.False(Leave.CanSettleLeftover(1405, 1000, closingYear: null, today));
        Assert.False(Leave.CanCarryLeftover(1405, 1000, closingYear: null, today));
        Assert.True(Leave.CanSettleLeftover(1404, 1000, closingYear: null, today));
        Assert.True(Leave.CanCarryLeftover(1404, 1000, closingYear: null, today));
        Assert.False(Leave.CanSettleLeftover(1404, 0, closingYear: null, today));
        Assert.False(Leave.CanCarryLeftover(1404, 0, closingYear: null, today));
        Assert.True(Leave.CanSettleLeftover(1405, 1000, closingYear: 1405, today));
        Assert.False(Leave.CanCarryLeftover(1405, 1000, closingYear: 1405, today));
        Assert.Equal(Leave.CarryLeaveMinutes, Leave.SuggestedTransferMinutes(8000, canTransfer: true));
        Assert.Equal(0, Leave.SuggestedTransferMinutes(8000, canTransfer: false));
        Assert.Equal(500, Leave.SuggestedTransferMinutes(500, canTransfer: true));

        Leave.ValidateLeftoverAllocation(1404, 8000, 900, 1100, closingYear: null, today);
        ArgumentException closed = Assert.Throws<ArgumentException>(
            () => Leave.ValidateLeftoverAllocation(1404, 8000, 100, 0, closingYear: 1404, today));
        Assert.Equal(Leave.LeftoverClosingNoTransfer, closed.Message);
        ArgumentException exceeds = Assert.Throws<ArgumentException>(
            () => Leave.ValidateLeftoverAllocation(1404, 8000, 5000, 4000, closingYear: null, today));
        Assert.Equal(Leave.LeftoverExceeds, exceeds.Message);
        ArgumentException empty = Assert.Throws<ArgumentException>(
            () => Leave.ValidateLeftoverAllocation(1404, 8000, 0, 0, closingYear: null, today));
        Assert.Equal(Leave.LeftoverNeedAmount, empty.Message);
        ArgumentException openYear = Assert.Throws<ArgumentException>(
            () => Leave.ValidateLeftoverAllocation(1405, 8000, 0, 1000, closingYear: null, today));
        Assert.Equal(Leave.LeftoverOpenYear, openYear.Message);
        Leave.ValidateLeftoverAllocation(1405, 8000, 0, 2000, closingYear: 1405, today);
    }
}
