using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.App.Settings;

public sealed partial class SettingsForm : UserControl
{
    public SettingsForm()
    {
        InitializeComponent();
        AdvancedButton.Content = AppMessages.AdvancedSettings;
        AdvancedButton.Loaded += OnAdvancedButtonLoaded;
        ThemeSwitch.Preference = Theme.Preference;
        ThemeSwitch.PreferenceChanged += OnPreferenceChanged;
    }

    public event EventHandler? AdvancedRequested;

    private void OnAdvancedButtonLoaded(object sender, RoutedEventArgs args)
    {
        AdvancedButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        AdvancedButton.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private void OnAdvanced(object sender, RoutedEventArgs args)
    {
        AdvancedRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreferenceChanged(object? sender, EventArgs args)
    {
        if (App.Current.HostWindow?.Content is FrameworkElement root)
        {
            Theme.Apply(root, ThemeSwitch.Preference);
        }
    }
}
