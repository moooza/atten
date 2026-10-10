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
        SizeWindow();
        if (Content is FrameworkElement root)
        {
            Theme.ApplySaved(root);
        }

        BuildNavigation();
        BindThemePicker();
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

    private void BindThemePicker()
    {
        ThemePicker.Items.Add("روشن");
        ThemePicker.Items.Add("تیره");
        ThemePicker.SelectedIndex = Theme.Current == AppTheme.Dark ? 1 : 0;
        ThemePicker.SelectionChanged += OnThemePicked;
    }

    private void OnThemePicked(object sender, SelectionChangedEventArgs args)
    {
        if (Content is not FrameworkElement root)
        {
            return;
        }

        AppTheme theme = ThemePicker.SelectedIndex == 1 ? AppTheme.Dark : AppTheme.Light;
        Theme.Apply(root, theme);
    }
}
