using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;

namespace Atten.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Theme.Load();
        RequestedTheme = Theme.Current == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }

    public static new App Current => (App)Application.Current;

    public IAttenRepository? Store { get; private set; }

    public Window? HostWindow => _window;

    public void ReloadStore()
    {
        Store = AttenStore.Open();
        ApplyOfficialHolidayCatalog();
    }

    /// <summary>
    /// Hands the official-holiday catalog to every Shamsi picker.
    /// Theme paints those days; it does not query Data.
    /// </summary>
    public void ApplyOfficialHolidayCatalog()
    {
        IReadOnlyList<OfficialHolidayRecord> rows = Store is null
            ? []
            : Store.OfficialHolidays.List();
        ShamsiCalendar.SharedOfficialDates = OfficialHoliday.Catalog(rows.Select(row => row.Date));
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string? notice = null;
        try
        {
            Store = AttenStore.Start(out notice);
            ApplyOfficialHolidayCatalog();
        }
        catch (Exception)
        {
            notice = AppMessages.StartupFailed;
            ShamsiCalendar.SharedOfficialDates = OfficialHoliday.Catalog(Array.Empty<string>());
        }

        _window = new MainWindow(notice);
        _window.Activate();
    }
}
