using Atten.Core;
using Atten.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Atten.App.Dashboard;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs args)
    {
        base.OnNavigatedTo(args);
        Reload();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        App.Current.OfficialHolidaysChanged += OnOfficialHolidaysChanged;
        Reload();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        App.Current.OfficialHolidaysChanged -= OnOfficialHolidaysChanged;
    }

    private void OnOfficialHolidaysChanged(object? sender, EventArgs args)
    {
        Reload();
    }

    private void Reload()
    {
        int year = Leave.ShamsiYearOf(DateOnly.FromDateTime(DateTime.Today));
        HolidayTitle.Text = $"{AppMessages.OfficialHolidays} {year}";
        IOfficialHolidayRepository? store = App.Current.Store?.OfficialHolidays;
        if (store is null)
        {
            ShowMissing(AppMessages.OfficialHolidayListFailed, hint: false);
            return;
        }

        try
        {
            var (start, end) = Leave.ShamsiYearSpan(year);
            int count = store.List(start, end).Count;
            if (count == 0)
            {
                ShowMissing(AppMessages.DashboardHolidayMissing, hint: true);
                return;
            }

            HolidayStatus.Text = $"{AppMessages.DashboardHolidayReady} {AppMessages.OfficialHolidayCount(count)}";
            ApplyTextStyle(HolidayStatus, "AppSuccessText");
            HolidayHint.Text = string.Empty;
            HolidayHint.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowMissing($"{AppMessages.OfficialHolidayListFailed}: {ex.Message}", hint: false);
        }
    }

    private void ShowMissing(string status, bool hint)
    {
        HolidayStatus.Text = status;
        ApplyTextStyle(HolidayStatus, "AppDangerText");
        HolidayHint.Text = hint ? AppMessages.DashboardHolidayMissingHint : string.Empty;
        HolidayHint.Visibility = hint ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ApplyTextStyle(TextBlock block, string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out object? resource) && resource is Style style)
        {
            block.Style = style;
        }

        block.HorizontalAlignment = HorizontalAlignment.Stretch;
        block.TextAlignment = TextAlignment.Start;
        block.FlowDirection = FlowDirection.RightToLeft;
    }
}
