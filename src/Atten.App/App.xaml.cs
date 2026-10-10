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
        AppTheme theme = Theme.Load();
        RequestedTheme = theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }

    public static new App Current => (App)Application.Current;

    public IAttenRepository? Store { get; private set; }

    public Window? HostWindow => _window;

    public void ReloadStore()
    {
        Store = AttenStore.Open();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string? notice = null;
        try
        {
            Store = AttenStore.Start(out notice);
        }
        catch (Exception)
        {
            notice = AppMessages.StartupFailed;
        }

        _window = new MainWindow(notice);
        _window.Activate();
    }
}
