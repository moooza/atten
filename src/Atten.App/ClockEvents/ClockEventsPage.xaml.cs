using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace Atten.App.ClockEvents;

public sealed partial class ClockEventsPage : Page
{
    private readonly IClockEventRepository? _store;
    private int _page = 1;
    private int _pageSize = Paging.DefaultPageSize;

    public ClockEventsPage()
    {
        InitializeComponent();
        _store = App.Current.Store?.ClockEvents;
        var (year, month, _) = Dates.ShamsiYmd(DateOnly.FromDateTime(DateTime.Today));
        MonthField.SetMonth(year, month);
        PageSize.SelectedSize = _pageSize;
        MonthField.MonthChanged += (_, _) =>
        {
            _page = 1;
            Reload();
        };
        PageSize.PageSizeChanged += (_, _) =>
        {
            _page = 1;
            _pageSize = PageSize.SelectedSize;
            Reload();
        };
        Pager.PageChanged += (_, _) =>
        {
            _page = Pager.Page;
            Reload();
        };
        Loaded += (_, _) => Reload();
    }

    private async void OnImport(object sender, RoutedEventArgs args)
    {
        Window? host = App.Current.HostWindow;
        if (host is null || _store is null)
        {
            ShowNotice(AppMessages.ClockListFailed, danger: true);
            return;
        }

        var picker = new FileOpenPicker(host.AppWindow.Id)
        {
            CommitButtonText = "بارگذاری",
            Title = AppMessages.ClockPickerTitle,
        };
        picker.FileTypeFilter.Add(".dat");
        picker.FileTypeFilter.Add("*");

        PickFileResult? picked = await picker.PickSingleFileAsync();
        if (picked is null || string.IsNullOrWhiteSpace(picked.Path))
        {
            return;
        }

        ImportFile(picked.Path);
    }

    internal void ImportFile(string path)
    {
        if (_store is null)
        {
            ShowNotice(AppMessages.ClockListFailed, danger: true);
            return;
        }

        try
        {
            AttlogReadResult parsed = Attlog.Read(File.ReadAllBytes(path));
            ClockEventImport result = _store.Import(parsed.Rows);
            ShowImportResult(
                result.Added,
                result.Skipped,
                parsed.Errors.Concat(result.Errors).ToList());
            _page = 1;
            Reload();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowNotice(AppMessages.ClockFileReadFailed, danger: true);
        }
        catch (Exception ex)
        {
            ShowNotice(ex.Message, danger: true);
        }
    }

    private void Reload(bool clearNotice = true)
    {
        if (clearNotice)
        {
            Notice.Text = string.Empty;
        }

        if (_store is null)
        {
            CountText.Text = string.Empty;
            ShowEmpty(AppMessages.ClockListFailed, hideTools: true);
            return;
        }

        try
        {
            var (start, end) = SelectedMonthSpan();
            int total = _store.Count(start, end);
            _pageSize = PageSize.SelectedSize;
            _page = Paging.ClampPage(_page, total, _pageSize);
            CountText.Text = AppMessages.ClockCount(total);
            Pager.Bind(total, _page, _pageSize);
            if (total == 0)
            {
                ShowEmpty(MonthField.Year is null ? AppMessages.ClockEmpty : AppMessages.ClockEmptyMonth);
                return;
            }

            IReadOnlyList<ClockEventRow> rows = _store
                .List(start, end, Paging.Skip(_page, _pageSize), _pageSize)
                .Select(row => new ClockEventRow(row))
                .ToList();
            EventsList.ItemsSource = rows;
            EmptyText.Visibility = Visibility.Collapsed;
            TablePanel.Visibility = Visibility.Visible;
            PagerBar.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            CountText.Text = string.Empty;
            ShowEmpty($"{AppMessages.ClockListFailed}: {ex.Message}", hideTools: true);
        }
    }

    private (DateOnly? Start, DateOnly? End) SelectedMonthSpan()
    {
        if (MonthField.Year is not int year || MonthField.Month is not int month)
        {
            return (null, null);
        }

        return Dates.ShamsiMonthSpan(year, month);
    }

    private void ShowEmpty(string message, bool hideTools = false)
    {
        EmptyText.Text = message;
        EmptyText.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
        PagerBar.Visibility = hideTools ? Visibility.Collapsed : Visibility.Visible;
        EventsList.ItemsSource = null;
    }

    private void ShowImportResult(int added, int skipped, IReadOnlyList<AttlogIssue> errors)
    {
        Notice.Text = string.Empty;
        if (XamlRoot is null)
        {
            return;
        }

        _ = AppConfirmDialog.ShowNoticeAsync(
            XamlRoot,
            ActualTheme,
            AppMessages.ClockImportTitle,
            AppMessages.ClockImportLines(added, skipped, errors));
    }

    private void ShowNotice(string message, bool danger)
    {
        Notice.Text = message;
        string key = danger ? "AppDangerText" : "AppInkText";
        if (Application.Current.Resources.TryGetValue(key, out object? resource) && resource is Style style)
        {
            Notice.Style = style;
        }
    }
}
