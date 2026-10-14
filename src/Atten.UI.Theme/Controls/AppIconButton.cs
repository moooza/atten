using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

public sealed class AppIconButton : Button
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(AppIconButton),
        new PropertyMetadata("\uE713", OnGlyphChanged));

    private readonly FontIcon _icon = new()
    {
        FontFamily = Theme.IconFontFamily,
        FontSize = 16,
    };

    public AppIconButton()
    {
        Content = _icon;
        Loaded += OnLoaded;
        ActualThemeChanged += (_, _) => TintIcon();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ThemeLooks.Apply(this, "AppIconButton");
        SyncGlyph();
        TintIcon();
    }

    private static void OnGlyphChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppIconButton)sender).SyncGlyph();
    }

    private void SyncGlyph()
    {
        _icon.Glyph = Glyph;
    }

    private void TintIcon()
    {
        _icon.Foreground = Foreground;
    }
}
