using System.Globalization;
using Atten.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

/// <summary>
/// Previous / next and the current page. Page math comes from Core.Paging.
/// </summary>
public sealed class AppPager : UserControl
{
    private readonly AppButton _previous = new()
    {
        Content = "قبلی",
        Kind = AppButtonKind.CompactSecondary,
    };
    private readonly AppButton _next = new()
    {
        Content = "بعدی",
        Kind = AppButtonKind.CompactSecondary,
    };
    private readonly TextBlock _range = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _page = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
    };
    private int _total;
    private int _pageNumber = 1;
    private int _pageSize = Paging.DefaultPageSize;
    private bool _syncing;

    public AppPager()
    {
        FlowDirection = FlowDirection.RightToLeft;
        _previous.Click += (_, _) => Move(-1);
        _next.Click += (_, _) => Move(1);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(_previous);
        row.Children.Add(_range);
        row.Children.Add(_page);
        row.Children.Add(_next);
        Content = row;

        Loaded += (_, _) =>
        {
            ApplyCaption(_range);
            ApplyCaption(_page);
            Paint();
        };
    }

    public event EventHandler? PageChanged;

    public int Page => _pageNumber;

    public void Bind(int total, int page, int pageSize)
    {
        _syncing = true;
        _total = total < 0 ? 0 : total;
        _pageSize = Paging.NormalizeSize(pageSize);
        _pageNumber = Paging.ClampPage(page, _total, _pageSize);
        _syncing = false;
        Paint();
    }

    private void Move(int delta)
    {
        if (_syncing)
        {
            return;
        }

        int next = Paging.ClampPage(_pageNumber + delta, _total, _pageSize);
        if (next == _pageNumber)
        {
            return;
        }

        _pageNumber = next;
        Paint();
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Paint()
    {
        int pages = Paging.PageCount(_total, _pageSize);
        var (start, end) = Paging.Range(_total, _pageNumber, _pageSize);
        _range.Text = _total == 0
            ? string.Empty
            : $"{start.ToString(CultureInfo.InvariantCulture)}–{end.ToString(CultureInfo.InvariantCulture)} از {_total.ToString(CultureInfo.InvariantCulture)}";
        _page.Text = pages == 0
            ? string.Empty
            : $"صفحه {_pageNumber.ToString(CultureInfo.InvariantCulture)} از {pages.ToString(CultureInfo.InvariantCulture)}";
        _previous.IsEnabled = _pageNumber > 1;
        _next.IsEnabled = pages > 0 && _pageNumber < pages;
        Visibility = _total == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void ApplyCaption(TextBlock block)
    {
        if (Application.Current?.Resources.TryGetValue("AppCaptionText", out object? value) == true
            && value is Style style)
        {
            block.Style = style;
        }
    }
}
