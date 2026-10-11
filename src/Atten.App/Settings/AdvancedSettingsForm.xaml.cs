using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Settings;

public sealed partial class AdvancedSettingsForm : UserControl
{
    public AdvancedSettingsForm()
    {
        InitializeComponent();
        Heading.Text = AppMessages.AdvancedSettings;
        HolidaysButton.Content = AppMessages.OfficialHolidayManage;
        HolidaysButton.Loaded += OnHolidaysButtonLoaded;
    }

    public event EventHandler? HolidaysRequested;

    public event EventHandler? Cancelled;

    private void OnHolidaysButtonLoaded(object sender, RoutedEventArgs args)
    {
        HolidaysButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        HolidaysButton.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private void OnHolidays(object sender, RoutedEventArgs args)
    {
        HolidaysRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancel(object sender, RoutedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        args.Handled = true;
    }
}
