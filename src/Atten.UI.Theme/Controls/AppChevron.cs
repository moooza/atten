using Microsoft.UI.Xaml;

namespace Atten.UI.Theme;

/// <summary>
/// Down when the menu is closed, up when it is open. Shared by every dropdown.
/// </summary>
public sealed class AppChevron : AppFontIcon
{
    public const string DownGlyph = "\uE70D";
    public const string UpGlyph = "\uE70E";

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(AppChevron),
        new PropertyMetadata(false, OnIsOpenChanged));

    public AppChevron()
    {
        FontSize = 12;
        Glyph = DownGlyph;
        Loaded += (_, _) => Tint();
        ActualThemeChanged += (_, _) => Tint();
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private static void OnIsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var chevron = (AppChevron)sender;
        chevron.Glyph = chevron.IsOpen ? UpGlyph : DownGlyph;
    }

    private void Tint()
    {
        Foreground = Theme.Brush(this, Tokens.Ink);
    }
}
