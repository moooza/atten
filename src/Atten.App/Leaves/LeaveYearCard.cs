using Atten.Core;
using Microsoft.UI.Xaml;

namespace Atten.App.Leaves;

public sealed class LeaveYearCard
{
    public LeaveYearCard(LeaveYearBalance balance)
    {
        Year = balance.Year;
        Title = $"سال {balance.Year}";
        Notice = balance.MissingStart ? AppMessages.LeaveMissingStart : null;
        NoticeVisibility = balance.MissingStart ? Visibility.Visible : Visibility.Collapsed;
        Carry = Leave.FormatLeaveAmount(balance.Carry);
        Earned = Leave.FormatLeaveAmount(balance.Earned);
        Used = Leave.FormatLeaveAmount(balance.Used);
        Remaining = AppMessages.LeaveRemaining(balance.RemainingBeforeTransfer, balance.RemainingAfterTransfer);
        Transfer = Leave.FormatLeaveAmount(balance.Transfer);
    }

    public int Year { get; }

    public string Title { get; }

    public string? Notice { get; }

    public Visibility NoticeVisibility { get; }

    public string Carry { get; }

    public string Earned { get; }

    public string Used { get; }

    public string Remaining { get; }

    public string Transfer { get; }
}
