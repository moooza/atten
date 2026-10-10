using System.Globalization;
using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Leaves;

public sealed partial class LeaveForm : UserControl
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private ILeaveRepository? _store;
    private long? _leaveId;

    public LeaveForm()
    {
        InitializeComponent();
        Hint.Text = AppMessages.LeaveHint;
        DaysBox.TextChanged += (_, _) => UpdatePreview();
        HoursBox.TextChanged += (_, _) => UpdatePreview();
        MinutesBox.TextChanged += (_, _) => UpdatePreview();
    }

    public event EventHandler? Saved;

    public event EventHandler? Cancelled;

    public void Bind(
        ILeaveRepository store,
        IPersonnelRepository people,
        long? personnelId,
        LeaveRecord? leave,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        int minutes = 0)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(people);
        _store = store;
        _leaveId = leave?.Id;
        Heading.Text = leave is null ? AppMessages.LeaveAdd : AppMessages.LeaveEdit;
        SaveButton.Content = leave is null ? AppMessages.LeaveSaveNew : AppMessages.LeaveSaveEdit;
        ErrorText.Text = string.Empty;
        PersonPicker.Source = people;
        PersonPicker.SelectedPersonnelId = leave?.PersonnelId ?? personnelId;
        if (leave is not null)
        {
            ApplyAmount(leave.StartDate, leave.EndDate, leave.Minutes);
        }
        else if (startDate is not null)
        {
            ApplyAmount(Dates.StorageDate(startDate.Value), Dates.StorageDate(endDate ?? startDate.Value), minutes);
        }
        else
        {
            StartField.StorageDate = null;
            StartField.Text = string.Empty;
            EndField.StorageDate = null;
            EndField.Text = string.Empty;
            DaysBox.Text = string.Empty;
            HoursBox.Text = string.Empty;
            MinutesBox.Text = string.Empty;
        }

        UpdatePreview();
        PersonPicker.Focus(FocusState.Programmatic);
    }

    private void ApplyAmount(string startDate, string endDate, int minutes)
    {
        StartField.StorageDate = startDate;
        EndField.StorageDate = endDate;
        var (days, hours, mins) = Leave.SplitLeaveMinutes(minutes);
        DaysBox.Text = days.ToString(Invariant);
        HoursBox.Text = hours.ToString(Invariant);
        MinutesBox.Text = mins.ToString(Invariant);
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

    private void OnWholeNumberChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        args.Cancel = !PersonnelHours.AcceptsHours(args.NewText);
    }

    private void OnMinutesChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        args.Cancel = !PersonnelHours.AcceptsMinutes(args.NewText);
    }

    private void Save()
    {
        if (_store is null)
        {
            return;
        }

        try
        {
            if (PersonPicker.SelectedPersonnelId is not long personId)
            {
                throw new ArgumentException(AppMessages.ChoosePersonnel);
            }

            DateOnly start = RequiredDate(StartField);
            DateOnly end = RequiredDate(EndField);
            if (end < start)
            {
                throw new ArgumentException(AppMessages.LeaveEndBeforeStart);
            }

            int days = OptionalWhole(DaysBox.Text);
            int hours = OptionalWhole(HoursBox.Text);
            int minutes = OptionalWhole(MinutesBox.Text);
            if (minutes > 59)
            {
                throw new ArgumentException(PersonnelHours.MinutesRange);
            }

            int amount = Leave.ComposeLeaveMinutes(days, hours, minutes);
            if (_leaveId is null)
            {
                _store.Add(personId, start, end, amount);
            }
            else
            {
                _store.Update(_leaveId.Value, personId, start, end, amount);
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void UpdatePreview()
    {
        try
        {
            int days = OptionalWhole(DaysBox.Text);
            int hours = OptionalWhole(HoursBox.Text);
            int minutes = OptionalWhole(MinutesBox.Text);
            Preview.Text = AppMessages.LeavePreview(Leave.ComposeLeaveMinutes(days, hours, minutes));
        }
        catch (ArgumentException)
        {
            Preview.Text = string.Empty;
        }
    }

    private static DateOnly RequiredDate(ShamsiDateField field)
    {
        if (string.IsNullOrWhiteSpace(field.Text))
        {
            return Dates.ParseShamsiDate(field.Text);
        }

        return field.GregorianDate ?? Dates.ParseShamsiDate(field.Text);
    }

    private static int OptionalWhole(string? text)
    {
        string raw = text?.Trim() ?? string.Empty;
        if (raw.Length == 0)
        {
            return 0;
        }

        return PersonnelHours.WholeNumber(raw)
            ?? throw new ArgumentException(AppMessages.LeaveInvalidNumber);
    }
}
