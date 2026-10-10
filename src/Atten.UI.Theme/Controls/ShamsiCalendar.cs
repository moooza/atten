using System.Globalization;
using Atten.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

/// <summary>
/// Draws one Shamsi month. Month days, weekdays, and names come from Core.
/// </summary>
public sealed class ShamsiCalendar : UserControl
{
    private int _year;
    private int _month;
    private DateOnly _selected;
    private readonly TextBlock _monthLabel;
    private readonly Grid _days;

    public ShamsiCalendar()
    {
        FlowDirection = FlowDirection.RightToLeft;
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        _selected = today;
        (_year, _month, _) = Dates.ShamsiYmd(today);

        _monthLabel = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var previous = new AppButton
        {
            Content = "ماه قبل",
            Kind = AppButtonKind.Secondary,
            Padding = new Thickness(8, 4, 8, 4),
        };
        previous.Click += (_, _) => Shift(-1);

        var next = new AppButton
        {
            Content = "ماه بعد",
            Kind = AppButtonKind.Secondary,
            Padding = new Thickness(8, 4, 8, 4),
        };
        next.Click += (_, _) => Shift(1);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(previous, 0);
        Grid.SetColumn(_monthLabel, 1);
        Grid.SetColumn(next, 2);
        header.Children.Add(previous);
        header.Children.Add(_monthLabel);
        header.Children.Add(next);

        _days = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        for (int column = 0; column < 7; column++)
        {
            _days.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var root = new StackPanel { Spacing = 0 };
        root.Children.Add(header);
        root.Children.Add(_days);
        Content = root;
        MinWidth = 252;
        ActualThemeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    public event EventHandler<DateOnly>? DatePicked;

    public DateOnly SelectedDate
    {
        get => _selected;
        set
        {
            _selected = value;
            Show(value);
        }
    }

    public void Show(DateOnly selected)
    {
        _selected = selected;
        (_year, _month, _) = Dates.ShamsiYmd(selected);
        Render();
    }

    private void Shift(int delta)
    {
        int index = _year * 12 + (_month - 1) + delta;
        if (index < 12)
        {
            return;
        }

        _year = Math.DivRem(index, 12, out int monthIndex);
        _month = monthIndex + 1;
        Render();
    }

    private void Render()
    {
        _days.Children.Clear();
        _days.RowDefinitions.Clear();
        _monthLabel.Text = $"{Dates.ShamsiMonthNames[_month - 1]} {_year}";
        _monthLabel.Foreground = Theme.Brush(this, Tokens.Ink);

        _days.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int weekday = 0; weekday < 7; weekday++)
        {
            var label = new TextBlock
            {
                Text = Dates.ShamsiWeekdayNames[weekday][..1],
                Foreground = Theme.Brush(this, Tokens.Muted),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(2),
            };
            Grid.SetColumn(label, weekday);
            _days.Children.Add(label);
        }

        IReadOnlyList<DateOnly> monthDays = Dates.ShamsiMonthDates(_year, _month);
        int offset = Dates.ShamsiWeekIndex(monthDays[0]);
        int rows = 1 + (offset + monthDays.Count - 1) / 7;
        for (int row = 0; row < rows; row++)
        {
            _days.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int index = 0; index < monthDays.Count; index++)
        {
            DateOnly day = monthDays[index];
            var (_, _, dayNumber) = Dates.ShamsiYmd(day);
            bool chosen = day == _selected;
            var cell = new Button
            {
                Content = dayNumber.ToString(CultureInfo.InvariantCulture),
                Background = chosen ? Theme.Brush(this, Tokens.Accent) : Theme.Brush(this, Tokens.Surface),
                Foreground = chosen ? Theme.Brush(this, Tokens.OnAccent) : Theme.Brush(this, Tokens.Ink),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MinWidth = 32,
                MinHeight = 32,
                CornerRadius = new CornerRadius(4),
                Tag = day,
            };
            cell.Click += OnDayClick;
            Grid.SetColumn(cell, (offset + index) % 7);
            Grid.SetRow(cell, 1 + (offset + index) / 7);
            _days.Children.Add(cell);
        }
    }

    private void OnDayClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: DateOnly day })
        {
            _selected = day;
            DatePicked?.Invoke(this, day);
        }
    }
}
