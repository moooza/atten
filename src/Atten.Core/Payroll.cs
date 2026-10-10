namespace Atten.Core;

/// <summary>
/// Monthly payroll snapshot and a one-user draft/confirm workflow.
/// Periods are Shamsi months on screen; stored bounds stay Gregorian.
/// </summary>
public static class Payroll
{
    public const string Draft = "draft";

    public const string Confirmed = "confirmed";

    private const string Locked = "حقوق تأییدشده را نمی‌توان دوباره محاسبه کرد.";
    private const string NotDraft = "فقط پیش‌نویس را می‌توان تأیید کرد.";
    private const string NotConfirmed = "فقط حقوق تأییدشده را می‌توان به پیش‌نویس برگرداند.";

    public static (DateOnly Start, DateOnly End) MonthSpan(int shamsiYear, int shamsiMonth)
    {
        IReadOnlyList<DateOnly> days = Dates.ShamsiMonthDates(shamsiYear, shamsiMonth);
        return (days[0], days[^1]);
    }

    public static string FormatPeriod(DateOnly start)
    {
        return FormatPeriod(Dates.ShamsiYmd(start));
    }

    public static string FormatPeriod(string start)
    {
        return FormatPeriod(Dates.ShamsiYmd(start));
    }

    private static string FormatPeriod((int Year, int Month, int Day) shamsi)
    {
        return $"{Dates.ShamsiMonthNames[shamsi.Month - 1]} {shamsi.Year}";
    }

    public static PayrollTotals Summarize(
        IEnumerable<int> balanceMinutes,
        int leaveMinutes,
        int remainingLeaveMinutes)
    {
        ArgumentNullException.ThrowIfNull(balanceMinutes);
        if (leaveMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leaveMinutes));
        }

        int overtime = 0;
        int deficit = 0;
        foreach (int minutes in balanceMinutes)
        {
            if (minutes > 0)
            {
                overtime += minutes;
            }
            else if (minutes < 0)
            {
                deficit += -minutes;
            }
        }

        return new PayrollTotals(overtime, deficit, leaveMinutes, remainingLeaveMinutes);
    }

    public static bool IsDraft(string status)
    {
        return string.Equals(status, Draft, StringComparison.Ordinal);
    }

    public static bool IsConfirmed(string status)
    {
        return string.Equals(status, Confirmed, StringComparison.Ordinal);
    }

    public static void EnsureCanEdit(string status)
    {
        if (IsConfirmed(status))
        {
            throw new ArgumentException(Locked);
        }
    }

    public static void EnsureCanConfirm(string status)
    {
        if (!IsDraft(status))
        {
            throw new ArgumentException(NotDraft);
        }
    }

    public static void EnsureCanReopen(string status)
    {
        if (!IsConfirmed(status))
        {
            throw new ArgumentException(NotConfirmed);
        }
    }

    public static string FormatStatus(string status)
    {
        return IsConfirmed(status) ? "تأیید شده" : "پیش‌نویس";
    }
}

public sealed record PayrollTotals(
    int OvertimeMinutes,
    int DeficitMinutes,
    int LeaveMinutes,
    int RemainingLeaveMinutes);
