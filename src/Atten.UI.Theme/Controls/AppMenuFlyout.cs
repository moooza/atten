using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

/// <summary>
/// Themed context menu with no window chrome.
/// </summary>
public sealed class AppMenuFlyout : MenuFlyout
{
    public AppMenuFlyout()
    {
        Opening += OnOpening;
    }

    private void OnOpening(object? sender, object args)
    {
        if (Application.Current?.Resources.TryGetValue("AppMenuFlyoutPresenter", out object? value) == true
            && value is Style style)
        {
            MenuFlyoutPresenterStyle = style;
        }
    }
}

public sealed class AppMenuItem : MenuFlyoutItem
{
    public AppMenuItem()
    {
        Loaded += (_, _) => ThemeLooks.Apply(this, "AppMenuFlyoutItem");
    }
}
