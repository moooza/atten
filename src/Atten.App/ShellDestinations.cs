using Atten.App.Backup;
using Atten.App.Calculation;
using Atten.App.ClockEvents;
using Atten.App.Dashboard;
using Atten.App.Leaves;
using Atten.App.Payroll;
using Atten.App.Personnel;

namespace Atten.App;

public sealed record ShellDestination(string Key, string Caption, string Glyph, Type PageType);

public static class ShellDestinations
{
    public static readonly ShellDestination Dashboard = new(
        "dashboard",
        "داشبورد",
        "\uE9D2",
        typeof(DashboardPage));

    public static readonly ShellDestination Personnel = new(
        "personnel",
        "پرسنل",
        "\uE716",
        typeof(PersonnelPage));

    public static readonly ShellDestination ClockEvents = new(
        "clock_events",
        "ورود و خروج",
        "\uE823",
        typeof(ClockEventsPage));

    public static readonly ShellDestination Leaves = new(
        "leaves",
        "مرخصی‌ها",
        "\uE787",
        typeof(LeavesPage));

    public static readonly ShellDestination Calculation = new(
        "calculation",
        "محاسبه",
        "\uE8EF",
        typeof(CalculationPage));

    public static readonly ShellDestination Payroll = new(
        "payroll",
        "حقوق",
        "\uE8D4",
        typeof(PayrollPage));

    public static readonly ShellDestination Backup = new(
        "backup",
        "پشتیبان",
        "\uE74E",
        typeof(BackupPage));

    public static IReadOnlyList<ShellDestination> All { get; } =
    [
        Dashboard,
        Personnel,
        ClockEvents,
        Leaves,
        Calculation,
        Payroll,
        Backup,
    ];
}
