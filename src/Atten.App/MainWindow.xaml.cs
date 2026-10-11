using Atten.App.Settings;
using Atten.UI.Theme;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Atten.App;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, AppNavButton> _navItems = new(StringComparer.Ordinal);
    private string? _active;
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
        Show(ShellDestinations.Dashboard.Key);
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
        ShellDestination destination = ShellDestinations.All.First(item => item.Key == key);
        if (_active == destination.Key && ContentFrame.Content is not null)
        {
            return;
        }

        _active = destination.Key;
        foreach ((string name, AppNavButton button) in _navItems)
        {
            button.IsSelected = name == destination.Key;
        }

        ContentFrame.Navigate(destination.PageType);
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

    private void OpenAdvancedSettings()
    {
        SettingsButton.Flyout?.Hide();
        if (_advancedWindow is null || _advancedForm is null)
        {
            _advancedForm = new AdvancedSettingsForm();
            _advancedForm.HolidaysRequested += OnHolidaysRequested;
            _advancedForm.Cancelled += OnAdvancedCancelled;
            _advancedWindow = new FormWindow(
                AppMessages.AdvancedSettings,
                _advancedForm,
                new SizeInt32(420, 480));
            _advancedWindow.Closed += OnAdvancedClosed;
        }

        _advancedWindow.BringForward();
    }

    private void OnHolidaysRequested(object? sender, EventArgs args)
    {
        OpenOfficialHolidays();
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
