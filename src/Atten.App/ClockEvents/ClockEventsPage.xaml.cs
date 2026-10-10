using Atten.Core;
using Atten.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace Atten.App.ClockEvents;

public sealed partial class ClockEventsPage : Page
{
    private readonly IClockEventRepository? _store;

    public ClockEventsPage()
    {
        InitializeComponent();
        _store = App.Current.Store?.ClockEvents;
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
            byte[] bytes = File.ReadAllBytes(path);
            ClockEventImport result = _store.Import(Attlog.Parse(bytes));
            ShowNotice(AppMessages.ClockImportNotice(result.Added, result.Skipped), danger: false);
            Reload(clearNotice: false);
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
            ShowEmpty(AppMessages.ClockListFailed);
            return;
        }

        try
        {
            IReadOnlyList<ClockEventRow> rows = _store.List().Select(row => new ClockEventRow(row)).ToList();
            CountText.Text = AppMessages.ClockCount(rows.Count);
            EventsList.ItemsSource = rows;
            if (rows.Count == 0)
            {
                ShowEmpty(AppMessages.ClockEmpty);
                return;
            }

            EmptyText.Visibility = Visibility.Collapsed;
            TablePanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            CountText.Text = string.Empty;
            ShowEmpty($"{AppMessages.ClockListFailed}: {ex.Message}");
        }
    }

    private void ShowEmpty(string message)
    {
        EmptyText.Text = message;
        EmptyText.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
        EventsList.ItemsSource = null;
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
