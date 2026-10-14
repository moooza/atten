using Atten.App.Backup;
using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;

namespace Atten.App;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        Theme.Load();
        AttenStore.SnapshotKeep = BackupPreferences.Load();
        RequestedTheme = Theme.Current == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }

    public static new App Current => (App)Application.Current;

    public IAttenRepository? Store { get; private set; }

    public Window? HostWindow => _window;

    public event EventHandler? OfficialHolidaysChanged;

    public void ReloadStore()
    {
        Store = AttenStore.Open();
        ApplyOfficialHolidayCatalog();
    }

    /// <summary>
    /// Migrate and open a healthy or missing file. Does not restore a damaged database.
    /// </summary>
    public bool TryStartStore(out string? notice)
    {
        notice = null;
        try
        {
            Store = AttenStore.Start();
            ApplyOfficialHolidayCatalog();
            return true;
        }
        catch (Exception)
        {
            Store = null;
            notice = AppMessages.StartupFailed;
            ShamsiCalendar.SharedOfficialDates = OfficialHoliday.Catalog(Array.Empty<string>());
            return false;
        }
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
        OfficialHolidaysChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

        DatabaseInspection inspection = AttenStore.Inspect();
        if (inspection.Status == DatabaseStatus.Damaged)
        {
            _window.ShowRecovery(DatabaseRecoveryRequest.Live(inspection.Snapshots));
            return;
        }

        if (!TryStartStore(out string? notice))
        {
            _window.SetNotice(notice);
        }

        _window.Show(ShellDestinations.Dashboard.Key);
    }
}
