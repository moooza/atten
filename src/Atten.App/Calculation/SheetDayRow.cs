using Atten.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.App.Calculation;

internal sealed class SheetDayRow
{
    public SheetDayRow(SheetRow row, int punchCount)
    {
        ArgumentNullException.ThrowIfNull(row);
        Record = row;
        DateKey = row.Date;
        Date = Dates.FormatShamsiDate(row.Date);
        Weekday = Dates.FormatShamsiWeekday(row.Date);
        Holiday = row.Holiday;
        Balance = Sheet.FormatBalance(row.BalanceMinutes);
        LeaveText = Atten.Core.Leave.FormatLeaveDuration(row.LeaveMinutes);
        Incomplete = row.Incomplete;
        OnLeave = row.OnLeave;
        CanAddLeave = row.BalanceMinutes < 0;
        var slots = new string[punchCount];
        for (int index = 0; index < punchCount; index++)
        {
            slots[index] = index < row.Slots.Count ? row.Slots[index] ?? string.Empty : string.Empty;
        }

        Slots = slots;
    }

    public SheetRow Record { get; }

    public string DateKey { get; }

    public string Date { get; }

    public string Weekday { get; }

    public bool Holiday { get; }

    public string Balance { get; }

    public string LeaveText { get; }

    public bool Incomplete { get; }

    public bool OnLeave { get; }

    public bool CanAddLeave { get; }

    public IReadOnlyList<string> Slots { get; }
}

internal static class SheetLayout
{
    public const double HolidayWidth = 88;
    public const double WeekdayWidth = 80;
    public const double DateWidth = 120;
    public const double PunchWidth = 100;
    public const double BalanceWidth = 120;
    public const double LeaveWidth = 180;
    public const double ActionsWidth = 220;

    public static int PunchCount(IEnumerable<SheetRow> rows)
    {
        int count = 0;
        foreach (SheetRow row in rows)
        {
            count = Math.Max(count, row.Slots.Count);
        }

        return count;
    }

    public static string PunchTitle(int slot)
    {
        return slot % 2 == 0 ? "ورود" : "خروج";
    }

    public static void AddColumns(Grid grid, int punchCount)
    {
        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(Fixed(HolidayWidth));
        grid.ColumnDefinitions.Add(Fixed(WeekdayWidth));
        grid.ColumnDefinitions.Add(Fixed(DateWidth));
        for (int slot = 0; slot < punchCount; slot++)
        {
            grid.ColumnDefinitions.Add(Fixed(PunchWidth));
        }

        grid.ColumnDefinitions.Add(Fixed(BalanceWidth));
        grid.ColumnDefinitions.Add(Fixed(LeaveWidth));
        grid.ColumnDefinitions.Add(Fixed(ActionsWidth));
    }

    public static int PunchColumn(int slot)
    {
        return 3 + slot;
    }

    public static int BalanceColumn(int punchCount)
    {
        return 3 + punchCount;
    }

    public static int LeaveColumn(int punchCount)
    {
        return 4 + punchCount;
    }

    public static int ActionsColumn(int punchCount)
    {
        return 5 + punchCount;
    }

    private static ColumnDefinition Fixed(double width)
    {
        return new ColumnDefinition { Width = new GridLength(width) };
    }
}
