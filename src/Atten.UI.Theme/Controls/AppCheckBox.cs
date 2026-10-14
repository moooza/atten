using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Themed checkbox at the app glyph size. Screens do not scale CheckBox themselves.
/// </summary>
public sealed class AppCheckBox : CheckBox
{
    public const double GlyphScale = 0.78;

    public AppCheckBox()
    {
        RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        RenderTransform = new ScaleTransform { ScaleX = GlyphScale, ScaleY = GlyphScale };
        Loaded += (_, _) => ThemeLooks.Apply(this, "AppCheckBox");
    }
}
