using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Three-way theme control: dark on the left, system in the center, light on the right.
/// Positions stay physical; this control does not follow RTL.
/// </summary>
public sealed class AppThemeSwitch : UserControl
{
    public static readonly DependencyProperty PreferenceProperty = DependencyProperty.Register(
        nameof(Preference),
        typeof(AppTheme),
        typeof(AppThemeSwitch),
        new PropertyMetadata(AppTheme.System, OnPreferenceChanged));

    private readonly Button _dark;
    private readonly Button _system;
    private readonly Button _light;
    private readonly FontIcon _darkIcon = Icon("\uE708");
    private readonly FontIcon _systemIcon = Icon("\uE7F4");
    private readonly FontIcon _lightIcon = Icon("\uE706");

    public AppThemeSwitch()
    {
        FlowDirection = FlowDirection.LeftToRight;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _dark = Part(_darkIcon, "تیره");
        _system = Part(_systemIcon, "سیستم");
        _light = Part(_lightIcon, "روشن");

        _dark.Click += (_, _) => Preference = AppTheme.Dark;
        _system.Click += (_, _) => Preference = AppTheme.System;
        _light.Click += (_, _) => Preference = AppTheme.Light;

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_dark, 0);
        Grid.SetColumn(_system, 1);
        Grid.SetColumn(_light, 2);
        row.Children.Add(_dark);
        row.Children.Add(_system);
        row.Children.Add(_light);

        var track = new Border { Child = row };
        Content = track;

        Loaded += (_, _) =>
        {
            ThemeLooks.Apply(track, "AppThemeSwitchTrack");
            SyncParts();
        };
        ActualThemeChanged += (_, _) => SyncParts();
    }

    public event EventHandler? PreferenceChanged;

    public AppTheme Preference
    {
        get => (AppTheme)GetValue(PreferenceProperty);
        set => SetValue(PreferenceProperty, value);
    }

    private static void OnPreferenceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (AppThemeSwitch)sender;
        control.SyncParts();
        control.PreferenceChanged?.Invoke(control, EventArgs.Empty);
    }

    private void SyncParts()
    {
        Paint(_dark, _darkIcon, Preference == AppTheme.Dark);
        Paint(_system, _systemIcon, Preference == AppTheme.System);
        Paint(_light, _lightIcon, Preference == AppTheme.Light);
    }

    private static void Paint(Button button, FontIcon icon, bool selected)
    {
        ThemeLooks.Apply(button, selected ? "AppThemeSwitchPartSelected" : "AppThemeSwitchPart");
        icon.Foreground = button.Foreground;
    }

    private static Button Part(FontIcon icon, string caption)
    {
        ToolTipService.SetToolTip(icon, caption);
        var button = new Button
        {
            Content = icon,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(button, caption);
        return button;
    }

    private static FontIcon Icon(string glyph)
    {
        return new FontIcon
        {
            FontFamily = Theme.IconFontFamily,
            FontSize = 16,
            Glyph = glyph,
        };
    }
}
