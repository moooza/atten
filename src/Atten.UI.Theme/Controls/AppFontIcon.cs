using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Icon glyph on Segoe MDL2 Assets, which Windows 10 already has.
/// Do not inherit Shabnam from the parent button; PUA glyphs then become empty squares.
/// </summary>
public class AppFontIcon : UserControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(AppFontIcon),
        new PropertyMetadata(string.Empty, OnGlyphChanged));

    private readonly TextBlock _text = new()
    {
        FontFamily = Theme.IconFontFamily,
        IsTextScaleFactorEnabled = false,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public AppFontIcon()
    {
        IsTabStop = false;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        FontFamily = Theme.IconFontFamily;
        Content = _text;
        _text.SetBinding(TextBlock.FontSizeProperty, Bind(nameof(FontSize)));
        _text.SetBinding(TextBlock.ForegroundProperty, Bind(nameof(Foreground)));
        _text.SetBinding(TextBlock.FlowDirectionProperty, Bind(nameof(FlowDirection)));
        Loaded += (_, _) => ApplyIconFont();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    private void ApplyIconFont()
    {
        FontFamily = Theme.IconFontFamily;
        _text.FontFamily = Theme.IconFontFamily;
    }

    private Binding Bind(string path)
    {
        return new Binding
        {
            Source = this,
            Path = new PropertyPath(path),
        };
    }

    private static void OnGlyphChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var icon = (AppFontIcon)sender;
        icon._text.Text = icon.Glyph ?? string.Empty;
    }
}
