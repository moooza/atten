using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.App.Settings;

public sealed partial class SettingsForm : UserControl
{
    public SettingsForm()
    {
        InitializeComponent();
        ThemeSwitch.Preference = Theme.Preference;
        ThemeSwitch.PreferenceChanged += OnPreferenceChanged;
    }

    private void OnPreferenceChanged(object? sender, EventArgs args)
    {
        if (App.Current.HostWindow?.Content is FrameworkElement root)
        {
            Theme.Apply(root, ThemeSwitch.Preference);
        }
    }
}
