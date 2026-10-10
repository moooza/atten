using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// ListView that keeps the column header pinned and reapplies theme tokens to rows.
/// </summary>
public sealed class AppListView : ListView
{
    private FrameworkElement? _headerHost;
    private ScrollViewer? _scrollViewer;

    public AppListView()
    {
        ThemeLooks.Apply(this, "AppListViewStyle");
        Loaded += (_, _) => SyncHeaderGutter();
        ActualThemeChanged += (_, _) => RefreshChrome();
    }

    protected override void OnApplyTemplate()
    {
        if (_scrollViewer is not null)
        {
            _scrollViewer.SizeChanged -= OnScrollSize;
            _scrollViewer.ViewChanged -= OnScrollView;
        }

        base.OnApplyTemplate();
        _headerHost = GetTemplateChild("HeaderHost") as FrameworkElement;
        _scrollViewer = GetTemplateChild("ScrollViewer") as ScrollViewer;
        if (_scrollViewer is not null)
        {
            _scrollViewer.SizeChanged += OnScrollSize;
            _scrollViewer.ViewChanged += OnScrollView;
        }

        SyncHeaderGutter();
    }

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is ListViewItem row)
        {
            ApplyChrome(row);
        }
    }

    private void OnScrollSize(object sender, SizeChangedEventArgs args)
    {
        SyncHeaderGutter();
    }

    private void OnScrollView(object? sender, ScrollViewerViewChangedEventArgs args)
    {
        SyncHeaderGutter();
    }

    private void SyncHeaderGutter()
    {
        if (_headerHost is null || _scrollViewer is null)
        {
            return;
        }

        double gutter = _scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible
            ? VerticalBarWidth(_scrollViewer)
            : 0;
        _headerHost.Margin = new Thickness(gutter, 0, 0, 0);
    }

    private static double VerticalBarWidth(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is ScrollBar { Orientation: Orientation.Vertical } bar && bar.ActualWidth > 0)
            {
                return bar.ActualWidth;
            }

            double nested = VerticalBarWidth(child);
            if (nested > 0)
            {
                return nested;
            }
        }

        return 12;
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
