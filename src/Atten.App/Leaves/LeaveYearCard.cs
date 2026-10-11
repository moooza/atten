using Atten.Core;
using Microsoft.UI.Xaml;

namespace Atten.App.Leaves;

public sealed class LeaveYearCard
{
    public LeaveYearCard(LeaveYearBalance balance, int? closingYear)
    {
        Year = balance.Year;
        Title = $"سال {balance.Year}";
        Notice = balance.MissingStart ? AppMessages.LeaveMissingStart : null;
        NoticeVisibility = balance.MissingStart ? Visibility.Visible : Visibility.Collapsed;
        Carry = Leave.FormatLeaveAmount(balance.Carry);
        Earned = Leave.FormatLeaveAmount(balance.Earned);
        Used = Leave.FormatLeaveAmount(balance.Used);
        RemainingMinutes = balance.RemainingBeforeTransfer;
        ClosingYear = closingYear;
        Remaining = Leave.FormatSignedLeaveAmount(balance.RemainingBeforeTransfer);
        RemainingOkVisibility = balance.RemainingBeforeTransfer < 0 ? Visibility.Collapsed : Visibility.Visible;
        RemainingDangerVisibility = balance.RemainingBeforeTransfer < 0 ? Visibility.Visible : Visibility.Collapsed;
        Transfer = Leave.FormatLeaveAmount(balance.Transfer);
        Leftover = Leave.FormatLeaveAmount(balance.RemainingAfterTransfer);
        TransferLine = $"انتقال به سال بعد: {Transfer}";
        LeftoverLine = $"مانده بعد از انتقال: {Leftover}";
        bool canSettle = Leave.CanSettleLeftover(balance.Year, balance.RemainingBeforeTransfer, closingYear);
        CanTransfer = Leave.CanCarryLeftover(balance.Year, balance.RemainingBeforeTransfer, closingYear);
        ActionsVisibility = canSettle || CanTransfer ? Visibility.Visible : Visibility.Collapsed;
    }

    public int Year { get; }

    public string Title { get; }

    public string? Notice { get; }

    public Visibility NoticeVisibility { get; }

    public string Carry { get; }

    public string Earned { get; }

    public string Used { get; }

    public int RemainingMinutes { get; }

    public int? ClosingYear { get; }

    public bool CanTransfer { get; }

    public string Remaining { get; }

    public Visibility RemainingOkVisibility { get; }

    public Visibility RemainingDangerVisibility { get; }

    public string Transfer { get; }

    public string Leftover { get; }

    public string TransferLine { get; }

    public string LeftoverLine { get; }

    public Visibility ActionsVisibility { get; }
}
