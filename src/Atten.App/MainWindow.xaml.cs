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
        AppWindow.Resize(new SizeInt32(960, 560));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 760;
            presenter.PreferredMinimumHeight = 480;
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
        SettingsButton.Flyout = new AppPopup
        {
            Content = new SettingsForm(),
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom,
        };
    }
}
