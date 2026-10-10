using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

public enum AppButtonKind
{
    Primary,
    Secondary,
    Danger,
    CompactPrimary,
    CompactSecondary,
}

public sealed class AppButton : Button
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(AppButtonKind),
        typeof(AppButton),
        new PropertyMetadata(AppButtonKind.Primary, OnKindChanged));

    public AppButton()
    {
        Loaded += (_, _) => ApplyKindStyle();
    }

    public AppButtonKind Kind
    {
        get => (AppButtonKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static void OnKindChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppButton)sender).ApplyKindStyle();
    }

    private void ApplyKindStyle()
    {
        string key = Kind switch
        {
            AppButtonKind.Danger => "AppButtonDanger",
            AppButtonKind.Secondary => "AppButtonSecondary",
            AppButtonKind.CompactPrimary => "AppButtonCompactPrimary",
            AppButtonKind.CompactSecondary => "AppButtonCompactSecondary",
            _ => "AppButtonPrimary",
        };
        ThemeLooks.Apply(this, key);
    }
}
