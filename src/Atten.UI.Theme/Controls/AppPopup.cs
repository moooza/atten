using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

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
        Opened += OnOpened;
    }

    private void OnOpening(object? sender, object args)
    {
        if (Application.Current?.Resources.TryGetValue("AppPopupPresenter", out object? value) == true
            && value is Style style)
        {
            FlyoutPresenterStyle = style;
        }
    }

    public double MinPresenterWidth { get; set; }

    private void OnOpened(object? sender, object args)
    {
        if (Content is not FrameworkElement content)
        {
            return;
        }

        DependencyObject? current = content;
        while (current is not null)
        {
            if (current is FlyoutPresenter presenter)
            {
                presenter.FlowDirection = FlowDirection.RightToLeft;
                presenter.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                FitToAnchor(presenter, content);
                break;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private void FitToAnchor(FlyoutPresenter presenter, FrameworkElement content)
    {
        if (MinPresenterWidth <= 0)
        {
            return;
        }

        presenter.MinWidth = MinPresenterWidth;
        double inner = MinPresenterWidth - presenter.Padding.Left - presenter.Padding.Right;
        if (inner > 0)
        {
            content.MinWidth = inner;
            content.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }
}
