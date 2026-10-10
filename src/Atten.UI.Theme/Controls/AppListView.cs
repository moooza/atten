using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// ListView that reapplies theme tokens to realized rows.
/// The default item presenter keeps the previous theme until a hover restyles that row.
/// </summary>
public sealed class AppListView : ListView
{
    public AppListView()
    {
        ActualThemeChanged += (_, _) => RefreshChrome();
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ListViewItem row)
        {
            ApplyChrome(row);
        }
    }

    private void RefreshChrome()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            if (ContainerFromIndex(index) is ListViewItem row)
            {
                ApplyChrome(row);
            }
        }
    }

    private static void ApplyChrome(ListViewItem row)
    {
        row.Background = Theme.Brush(row, Tokens.Surface);
        row.Foreground = Theme.Brush(row, Tokens.Ink);
        if (VisualTreeHelper.GetChildrenCount(row) > 0
            && VisualTreeHelper.GetChild(row, 0) is ListViewItemPresenter presenter)
        {
            presenter.Background = Theme.Brush(row, Tokens.Surface);
            presenter.Foreground = Theme.Brush(row, Tokens.Ink);
            presenter.PointerOverBackground = Theme.Brush(row, Tokens.Line);
            presenter.PressedBackground = Theme.Brush(row, Tokens.Line);
            presenter.SelectedBackground = Theme.Brush(row, Tokens.Highlight);
            presenter.SelectedPointerOverBackground = Theme.Brush(row, Tokens.HighlightStrong);
            presenter.SelectedPressedBackground = Theme.Brush(row, Tokens.Highlight);
            presenter.SelectedForeground = Theme.Brush(row, Tokens.Ink);
            presenter.SelectionIndicatorBrush = Theme.Brush(row, Tokens.Accent);
        }

        VisualStateManager.GoToState(row, row.IsSelected ? "Selected" : "Normal", false);
    }
}
