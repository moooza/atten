using Atten.App.Backup;
using Atten.App.Settings;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;

namespace Atten.App;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, AppNavButton> _navItems = new(StringComparer.Ordinal);
    private string? _active;
    private string? _returnAfterPreview;
    private bool _shellLocked;
    private DatabaseRecoveryPage? _recoveryPage;
    private FormWindow? _advancedWindow;
    private AdvancedSettingsForm? _advancedForm;
    private FormWindow? _holidaysWindow;
    private OfficialHolidaysForm? _holidaysForm;

    public MainWindow(string? notice = null)
    {
        InitializeComponent();
        Title = AppInfo.WindowTitle;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        SizeWindow();
        if (Content is FrameworkElement root)
        {
            Theme.ApplySaved(root);
        }

        Theme.Changed += (_, _) => WindowChrome.Apply(this);
        WindowChrome.Apply(this);
        Closed += (_, _) =>
        {
            WindowPlacement.Save(AppWindow);
            _holidaysWindow?.Close();
            _advancedWindow?.Close();
        };
        BuildNavigation();
        BindSettings();
        SetNotice(notice);
    }

    public void SetNotice(string? notice)
    {
        if (string.IsNullOrWhiteSpace(notice))
        {
            NoticeBanner.Text = string.Empty;
            NoticeBanner.Visibility = Visibility.Collapsed;
            return;
        }

        NoticeBanner.Text = notice;
        NoticeBanner.Visibility = Visibility.Visible;
    }

    public void Show(string key)
    {
        if (_shellLocked)
        {
            return;
        }

        ShellDestination destination = ShellDestinations.All.First(item => item.Key == key);
        if (_active == destination.Key && ContentFrame.Content is not null)
        {
            return;
        }

        DetachRecoveryPage();
        _active = destination.Key;
        foreach ((string name, AppNavButton button) in _navItems)
        {
            button.IsSelected = name == destination.Key;
        }

        ContentFrame.Navigate(destination.PageType);
    }

    /// <summary>
    /// Recovery is not a shell destination. Nav and settings stay off until restore or preview close.
    /// </summary>
    public void ShowRecovery(DatabaseRecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _returnAfterPreview = request.Preview ? _active : null;
        SetShellLocked(true);
        _active = null;
        ContentFrame.Navigated -= OnRecoveryFrameNavigated;
        ContentFrame.Navigated += OnRecoveryFrameNavigated;
        ContentFrame.Navigate(typeof(DatabaseRecoveryPage), request);
        AttachRecoveryPage();
    }

    private void OnRecoveryFrameNavigated(object sender, NavigationEventArgs args)
    {
        ContentFrame.Navigated -= OnRecoveryFrameNavigated;
        AttachRecoveryPage();
    }

    private void SizeWindow()
    {
        WindowPlacement.Apply(AppWindow);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = WindowPlacement.MinWidth;
            presenter.PreferredMinimumHeight = WindowPlacement.MinHeight;
        }
    }

    private void BuildNavigation()
    {
        foreach (ShellDestination destination in ShellDestinations.All)
        {
            var button = new AppNavButton
            {
                Glyph = destination.Glyph,
                Caption = destination.Caption,
            };
            string key = destination.Key;
            button.Click += (_, _) => Show(key);
            _navItems[key] = button;
            NavBar.Children.Add(button);
        }
    }

    private void BindSettings()
    {
        var form = new SettingsForm();
        form.AdvancedRequested += (_, _) => OpenAdvancedSettings();
        SettingsButton.Flyout = new AppPopup
        {
            Content = form,
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom,
        };
    }

    private void SetShellLocked(bool locked)
    {
        _shellLocked = locked;
        foreach (AppNavButton button in _navItems.Values)
        {
            button.IsEnabled = !locked;
            if (locked)
            {
                button.IsSelected = false;
            }
        }

        SettingsButton.IsEnabled = !locked;
        if (locked)
        {
            SettingsButton.Flyout?.Hide();
        }
    }

    private void AttachRecoveryPage()
    {
        DetachRecoveryPage();
        if (ContentFrame.Content is not DatabaseRecoveryPage page)
        {
            return;
        }

        _recoveryPage = page;
        page.Restored += OnRecoveryRestored;
        page.PreviewClosed += OnRecoveryPreviewClosed;
    }

    private void DetachRecoveryPage()
    {
        if (_recoveryPage is null)
        {
            return;
        }

        _recoveryPage.Restored -= OnRecoveryRestored;
        _recoveryPage.PreviewClosed -= OnRecoveryPreviewClosed;
        _recoveryPage = null;
    }

    private void OnRecoveryRestored(object? sender, EventArgs args)
    {
        if (_recoveryPage is { Preview: true })
        {
            return;
        }

        if (!App.Current.TryStartStore(out string? notice))
        {
            SetNotice(notice);
            return;
        }

        DetachRecoveryPage();
        SetShellLocked(false);
        SetNotice(null);
        Show(ShellDestinations.Dashboard.Key);
    }

    private void OnRecoveryPreviewClosed(object? sender, EventArgs args)
    {
        string key = _returnAfterPreview ?? ShellDestinations.Dashboard.Key;
        _returnAfterPreview = null;
        DetachRecoveryPage();
        SetShellLocked(false);
        Show(key);
    }

    private void OpenAdvancedSettings()
    {
        if (_shellLocked)
        {
            return;
        }

        SettingsButton.Flyout?.Hide();
        if (_advancedWindow is null || _advancedForm is null)
        {
            _advancedForm = new AdvancedSettingsForm();
            _advancedForm.HolidaysRequested += OnHolidaysRequested;
            _advancedForm.PreviewRecoveryWithSnapshotsRequested += OnPreviewRecoveryWithSnapshots;
            _advancedForm.PreviewRecoveryWithoutSnapshotsRequested += OnPreviewRecoveryWithoutSnapshots;
            _advancedForm.Cancelled += OnAdvancedCancelled;
            _advancedWindow = new FormWindow(
                AppMessages.AdvancedSettings,
                _advancedForm,
                new SizeInt32(420, 720));
            _advancedWindow.Closed += OnAdvancedClosed;
        }

        _advancedWindow.BringForward();
    }

    private void OnHolidaysRequested(object? sender, EventArgs args)
    {
        OpenOfficialHolidays();
    }

    private void OnPreviewRecoveryWithSnapshots(object? sender, EventArgs args)
    {
        IReadOnlyList<BackupSnapshot> snapshots = App.Current.Store?.Backup.ListSnapshots()
            ?? AttenStore.Backup().ListSnapshots();
        if (snapshots.Count == 0)
        {
            snapshots = DatabaseRecoveryRequest.SampleSnapshots();
        }

        ShowRecoveryPreview(DatabaseRecoveryRequest.PreviewWithSnapshots(snapshots));
    }

    private void OnPreviewRecoveryWithoutSnapshots(object? sender, EventArgs args)
    {
        ShowRecoveryPreview(DatabaseRecoveryRequest.PreviewWithoutSnapshots());
    }

    private void ShowRecoveryPreview(DatabaseRecoveryRequest request)
    {
        _holidaysWindow?.Close();
        _advancedWindow?.Close();
        ShowRecovery(request);
    }

    private void OpenOfficialHolidays()
    {
        if (_holidaysWindow is null || _holidaysForm is null)
        {
            _holidaysForm = new OfficialHolidaysForm();
            _holidaysForm.Cancelled += OnHolidaysCancelled;
            _holidaysWindow = new FormWindow(
                AppMessages.OfficialHolidays,
                _holidaysForm,
                AppWindow.Size);
            _holidaysWindow.Closed += OnHolidaysClosed;
        }

        _holidaysWindow.BringForward();
    }

    private void OnAdvancedCancelled(object? sender, EventArgs args)
    {
        _advancedWindow?.Close();
    }

    private void OnHolidaysCancelled(object? sender, EventArgs args)
    {
        _holidaysWindow?.Close();
    }

    private void OnAdvancedClosed(object sender, WindowEventArgs args)
    {
        if (_advancedForm is not null)
        {
            _advancedForm.HolidaysRequested -= OnHolidaysRequested;
            _advancedForm.PreviewRecoveryWithSnapshotsRequested -= OnPreviewRecoveryWithSnapshots;
            _advancedForm.PreviewRecoveryWithoutSnapshotsRequested -= OnPreviewRecoveryWithoutSnapshots;
            _advancedForm.Cancelled -= OnAdvancedCancelled;
        }

        _advancedWindow = null;
        _advancedForm = null;
    }

    private void OnHolidaysClosed(object sender, WindowEventArgs args)
    {
        if (_holidaysForm is not null)
        {
            _holidaysForm.Cancelled -= OnHolidaysCancelled;
        }

        _holidaysWindow = null;
        _holidaysForm = null;
    }
}
