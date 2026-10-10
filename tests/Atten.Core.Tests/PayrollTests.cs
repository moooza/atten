using Xunit;

namespace Atten.Core.Tests;

public class PayrollTests
{
    [Fact]
    public void MonthSpanUsesTheGregorianBoundsOfAShamsiMonth()
    {
        var (start, end) = Payroll.MonthSpan(1405, 7);
        IReadOnlyList<DateOnly> days = Dates.ShamsiMonthDates(1405, 7);
        Assert.Equal(days[0], start);
        Assert.Equal(days[^1], end);
        Assert.Equal("2026-09-23", Dates.StorageDate(start));
        Assert.Equal("مهر 1405", Payroll.FormatPeriod(start));
        Assert.Equal("مهر 1405", Payroll.FormatPeriod(Dates.StorageDate(start)));
    }

    [Fact]
    public void SummarizeSplitsOvertimeAndDeficitAndKeepsLeave()
    {
        PayrollTotals totals = Payroll.Summarize([60, -30, 10, 0, -15], leaveMinutes: 440, remainingLeaveMinutes: 120);
        Assert.Equal(70, totals.OvertimeMinutes);
        Assert.Equal(45, totals.DeficitMinutes);
        Assert.Equal(440, totals.LeaveMinutes);
        Assert.Equal(120, totals.RemainingLeaveMinutes);
        Assert.Equal(new PayrollTotals(0, 0, 0, 0), Payroll.Summarize([], 0, 0));
    }

    [Fact]
    public void SummarizeRejectsNegativeLeaveMinutes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Payroll.Summarize([], -1, 0));
    }

    [Fact]
    public void OneUserWorkflowLocksAConfirmedRun()
    {
        Assert.True(Payroll.IsDraft(Payroll.Draft));
        Assert.True(Payroll.IsConfirmed(Payroll.Confirmed));
        Assert.Equal("پیش‌نویس", Payroll.FormatStatus(Payroll.Draft));
        Assert.Equal("تأیید شده", Payroll.FormatStatus(Payroll.Confirmed));

        Payroll.EnsureCanEdit(Payroll.Draft);
        Payroll.EnsureCanConfirm(Payroll.Draft);
        ArgumentException locked = Assert.Throws<ArgumentException>(() => Payroll.EnsureCanEdit(Payroll.Confirmed));
        Assert.Equal("حقوق تأییدشده را نمی‌توان دوباره محاسبه کرد.", locked.Message);
        ArgumentException notDraft = Assert.Throws<ArgumentException>(() => Payroll.EnsureCanConfirm(Payroll.Confirmed));
        Assert.Equal("فقط پیش‌نویس را می‌توان تأیید کرد.", notDraft.Message);

        Payroll.EnsureCanReopen(Payroll.Confirmed);
        ArgumentException notConfirmed = Assert.Throws<ArgumentException>(() => Payroll.EnsureCanReopen(Payroll.Draft));
        Assert.Equal("فقط حقوق تأییدشده را می‌توان به پیش‌نویس برگرداند.", notConfirmed.Message);
    }
}
