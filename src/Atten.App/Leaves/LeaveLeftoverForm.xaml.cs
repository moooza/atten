using System.Globalization;
using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Leaves;

public sealed partial class LeaveLeftoverForm : UserControl
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private ILeaveRepository? _store;
    private long _personnelId;
    private int _year;
    private int _remaining;
    private int? _closingYear;
    private bool _canTransfer;

    public LeaveLeftoverForm()
    {
        InitializeComponent();
        Hint.Text = AppMessages.LeaveLeftoverHint;
        TransferDaysBox.TextChanged += (_, _) => UpdatePreview();
        TransferHoursBox.TextChanged += (_, _) => UpdatePreview();
        TransferMinutesBox.TextChanged += (_, _) => UpdatePreview();
        SettleDaysBox.TextChanged += (_, _) => UpdatePreview();
        SettleHoursBox.TextChanged += (_, _) => UpdatePreview();
        SettleMinutesBox.TextChanged += (_, _) => UpdatePreview();
    }

    public event EventHandler? Saved;

    public event EventHandler? Cancelled;

    public void Bind(ILeaveRepository store, LeaveYearCard card, long personnelId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(card);
        _store = store;
        _personnelId = personnelId;
        _year = card.Year;
        _remaining = card.RemainingMinutes;
        _closingYear = card.ClosingYear;
        _canTransfer = card.CanTransfer;
        Heading.Text = AppMessages.LeaveLeftoverTitle(card.Year);
        RemainingText.Text = AppMessages.LeaveLeftoverRemaining(_remaining);
        ErrorText.Text = string.Empty;
        TransferPanel.Visibility = _canTransfer ? Visibility.Visible : Visibility.Collapsed;
        int transfer = Leave.SuggestedTransferMinutes(_remaining, _canTransfer);
        int settleMinutes = Math.Max(_remaining - transfer, 0);
        FillAmount(TransferDaysBox, TransferHoursBox, TransferMinutesBox, transfer);
        FillAmount(SettleDaysBox, SettleHoursBox, SettleMinutesBox, settleMinutes);
        UpdatePreview();
        if (_canTransfer)
        {
            TransferDaysBox.Focus(FocusState.Programmatic);
        }
        else
        {
            SettleDaysBox.Focus(FocusState.Programmatic);
        }
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
            int transfer = _canTransfer ? ReadAmount(TransferDaysBox, TransferHoursBox, TransferMinutesBox) : 0;
            int settle = ReadAmount(SettleDaysBox, SettleHoursBox, SettleMinutesBox);
            Leave.ValidateLeftoverAllocation(_year, _remaining, transfer, settle, _closingYear);
            _store.ApplyLeftover(_personnelId, _year, transfer, settle);
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
            int transfer = _canTransfer ? ReadAmount(TransferDaysBox, TransferHoursBox, TransferMinutesBox) : 0;
            int settle = ReadAmount(SettleDaysBox, SettleHoursBox, SettleMinutesBox);
            TransferPreview.Text = AppMessages.LeavePreview(transfer);
            SettlePreview.Text = AppMessages.LeavePreview(settle);
            UnallocatedText.Text = AppMessages.LeaveLeftoverUnallocated(_remaining - transfer - settle);
            ErrorText.Text = string.Empty;
        }
        catch (ArgumentException)
        {
            TransferPreview.Text = string.Empty;
            SettlePreview.Text = string.Empty;
            UnallocatedText.Text = string.Empty;
        }
    }

    private static void FillAmount(AppTextBox days, AppTextBox hours, AppTextBox minutes, int amount)
    {
        var (dayCount, hourCount, minuteCount) = Leave.SplitLeaveMinutes(amount);
        days.Text = amount == 0 ? string.Empty : dayCount.ToString(Invariant);
        hours.Text = amount == 0 ? string.Empty : hourCount.ToString(Invariant);
        minutes.Text = amount == 0 ? string.Empty : minuteCount.ToString(Invariant);
    }

    private static int ReadAmount(AppTextBox days, AppTextBox hours, AppTextBox minutes)
    {
        int dayCount = OptionalWhole(days.Text);
        int hourCount = OptionalWhole(hours.Text);
        int minuteCount = OptionalWhole(minutes.Text);
        if (minuteCount > 59)
        {
            throw new ArgumentException(PersonnelHours.MinutesRange);
        }

        return Leave.ComposeLeaveMinutes(dayCount, hourCount, minuteCount);
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
