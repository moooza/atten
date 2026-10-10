using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Atten.UI.Theme;

/// <summary>
/// Themed flyout with no title bar. Screens put checkboxes or other content here
/// instead of opening a dialog window.
/// </summary>
public sealed class AppPopup : Flyout
{
    public AppPopup()
    {
        Placement = FlyoutPlacementMode.Bottom;
        ShouldConstrainToRootBounds = false;
        LightDismissOverlayMode = LightDismissOverlayMode.Off;
        Opening += OnOpening;
    }

    private void OnOpening(object? sender, object args)
    {
        if (Application.Current?.Resources.TryGetValue("AppPopupPresenter", out object? value) == true
            && value is Style style)
        {
            FlyoutPresenterStyle = style;
        }
    }
}
