using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Settings;

public sealed partial class OfficialHolidayForm : UserControl
{
    private IOfficialHolidayRepository? _store;
    private long? _holidayId;

    public OfficialHolidayForm()
    {
        InitializeComponent();
        Hint.Text = AppMessages.OfficialHolidayHint;
        SingleRadio.Checked += OnKindChanged;
        RangeRadio.Checked += OnKindChanged;
        StartField.DateChanged += (_, _) => UpdatePreview();
        EndField.DateChanged += (_, _) => UpdatePreview();
    }

    public event EventHandler? Saved;

    public event EventHandler? Cancelled;

    public DateOnly? LastSavedDate { get; private set; }

    public void Bind(IOfficialHolidayRepository store, OfficialHolidayRecord? holiday)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _holidayId = holiday?.Id;
        LastSavedDate = null;
        Heading.Text = holiday is null ? AppMessages.OfficialHolidayAdd : AppMessages.OfficialHolidayEdit;
        ErrorText.Text = string.Empty;
        TitleBox.Text = holiday?.Title ?? string.Empty;
        DateField.StorageDate = holiday?.Date;
        if (holiday is null)
        {
            DateField.Text = string.Empty;
        }

        StartField.StorageDate = null;
        StartField.Text = string.Empty;
        EndField.StorageDate = null;
        EndField.Text = string.Empty;
        SingleRadio.IsChecked = true;
        RangeRadio.IsEnabled = holiday is null;
        ShowKind();
        DateField.Focus(FocusState.Programmatic);
    }

    private void OnKindChanged(object sender, RoutedEventArgs args)
    {
        if (RangePanel is null || SinglePanel is null)
        {
            return;
        }

        ShowKind();
        if (IsRange && StartField.GregorianDate is null && DateField.GregorianDate is DateOnly day)
        {
            StartField.GregorianDate = day;
        }

        UpdatePreview();
    }

    private void OnSave(object sender, RoutedEventArgs args)
    {
        Save();
    }

    private void OnCancel(object sender, RoutedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnEnterAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Save();
        args.Handled = true;
    }

    private void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        args.Handled = true;
    }

    private void Save()
    {
        if (_store is null)
        {
            return;
        }

        try
        {
            IReadOnlyList<DateOnly> days = ChosenDays();
            if (_holidayId is null)
            {
                if (days.Count == 1)
                {
                    _store.Add(days[0], TitleBox.Text);
                }
                else
                {
                    _store.AddMany(days, TitleBox.Text);
                }
            }
            else
            {
                _store.Update(_holidayId.Value, days[0], TitleBox.Text);
            }

            LastSavedDate = days[0];
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private IReadOnlyList<DateOnly> ChosenDays()
    {
        if (!IsRange)
        {
            return [RequiredDate(DateField, AppMessages.OfficialHolidayNeedDate)];
        }

        DateOnly start = RequiredDate(StartField, AppMessages.OfficialHolidayNeedStart);
        DateOnly end = RequiredDate(EndField, AppMessages.OfficialHolidayNeedEnd);
        try
        {
            return Dates.InclusiveDays(start, end);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException(AppMessages.OfficialHolidayEndBeforeStart);
        }
    }

    private void ShowKind()
    {
        bool range = IsRange;
        SinglePanel.Visibility = range ? Visibility.Collapsed : Visibility.Visible;
        RangePanel.Visibility = range ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (Preview is null)
        {
            return;
        }

        Preview.Text = string.Empty;
        if (!IsRange || StartField.GregorianDate is not DateOnly start || EndField.GregorianDate is not DateOnly end)
        {
            return;
        }

        try
        {
            Preview.Text = AppMessages.OfficialHolidayDays(Dates.InclusiveDays(start, end).Count);
        }
        catch (ArgumentException)
        {
            Preview.Text = string.Empty;
        }
    }

    private bool IsRange => RangeRadio is { IsChecked: true } && _holidayId is null;

    private static DateOnly RequiredDate(ShamsiDateField field, string message)
    {
        if (string.IsNullOrWhiteSpace(field.Text) && field.GregorianDate is null)
        {
            throw new ArgumentException(message);
        }

        return field.GregorianDate ?? Dates.ParseShamsiDate(field.Text);
    }
}
