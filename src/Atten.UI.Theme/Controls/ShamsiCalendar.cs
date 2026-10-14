using System.Globalization;
using Atten.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Days, then months, then years — same drill-down as the Windows calendar.
/// Month days, weekdays, and names come from Core.
/// </summary>
public sealed class ShamsiCalendar : UserControl
{
    private enum ViewMode
    {
        Days,
        Months,
        Years,
    }

    private const int YearSpan = 12;
    private const int FridayWeekday = 6;

    private static IReadOnlySet<string> _sharedOfficialDates = EmptyOfficialDates();

    private int _year;
    private int _month;
    private DateOnly _selected;
    private ViewMode _view = ViewMode.Days;
    private IReadOnlySet<string>? _officialDates;
    private readonly TextBlock _titleLabel;
    private readonly FontIcon _titleChevron;
    private readonly Button _titleButton;
    private readonly AppButton _previous;
    private readonly AppButton _next;
    private readonly Grid _cells;

    public ShamsiCalendar()
    {
        FlowDirection = FlowDirection.RightToLeft;
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        _selected = today;
        (_year, _month, _) = Dates.ShamsiYmd(today);

        _titleLabel = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _titleChevron = new FontIcon
        {
            FontFamily = Theme.IconFontFamily,
            FontSize = 12,
            Glyph = "\uE70D",
            Margin = new Thickness(6, 0, 0, 0),
        };
        var titleContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleContent.Children.Add(_titleLabel);
        titleContent.Children.Add(_titleChevron);

        _titleButton = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = titleContent,
        };
        ThemeLooks.Apply(_titleButton, "AppChromeButton");
        _titleButton.Click += (_, _) => DrillUp();

        _previous = new AppButton
        {
            Content = "ماه قبل",
            Kind = AppButtonKind.Secondary,
            Padding = new Thickness(8, 4, 8, 4),
        };
        _previous.Click += (_, _) => Shift(-1);

        _next = new AppButton
        {
            Content = "ماه بعد",
            Kind = AppButtonKind.Secondary,
            Padding = new Thickness(8, 4, 8, 4),
        };
        _next.Click += (_, _) => Shift(1);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_previous, 0);
        Grid.SetColumn(_titleButton, 1);
        Grid.SetColumn(_next, 2);
        header.Children.Add(_previous);
        header.Children.Add(_titleButton);
        header.Children.Add(_next);

        _cells = new Grid { Margin = new Thickness(0, 8, 0, 0) };

        var root = new StackPanel { Spacing = 0 };
        root.Children.Add(header);
        root.Children.Add(_cells);
        Content = root;
        MinWidth = 280;
        ActualThemeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    public event EventHandler<DateOnly>? DatePicked;

    public event EventHandler<(int Year, int Month)>? MonthPicked;

    /// <summary>
    /// Official holiday dates (Gregorian YYYY-MM-DD) for every picker.
    /// App sets this after the store opens. Theme does not query Data.
    /// </summary>
    public static IReadOnlySet<string> SharedOfficialDates
    {
        get => _sharedOfficialDates;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _sharedOfficialDates = value;
        }
    }

    /// <summary>
    /// Official dates this calendar paints with Danger. Defaults to
    /// <see cref="SharedOfficialDates"/> so every picker sees the same catalog.
    /// </summary>
    public IReadOnlySet<string> OfficialDates
    {
        get => _officialDates ?? SharedOfficialDates;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _officialDates = value;
            if (IsLoaded)
            {
                Render();
            }
        }
    }

    /// <summary>Month grid only. A month click picks the month instead of opening days.</summary>
    public bool PickMonth { get; set; }

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
        _view = PickMonth ? ViewMode.Months : ViewMode.Days;
        (_year, _month, _) = Dates.ShamsiYmd(selected);
        Render();
    }

    private void DrillUp()
    {
        _view = _view switch
        {
            ViewMode.Days => ViewMode.Months,
            ViewMode.Months => ViewMode.Years,
            _ => _view,
        };
        Render();
    }

    private void Shift(int delta)
    {
        if (_view == ViewMode.Days)
        {
            int index = _year * 12 + (_month - 1) + delta;
            if (index < 12)
            {
                return;
            }

            _year = Math.DivRem(index, 12, out int monthIndex);
            _month = monthIndex + 1;
            Render();
            return;
        }

        int step = _view == ViewMode.Years ? YearSpan : 1;
        int nextYear = _year + (delta * step);
        if (nextYear < 1)
        {
            return;
        }

        _year = nextYear;
        Render();
    }

    private void Render()
    {
        _cells.Children.Clear();
        _cells.RowDefinitions.Clear();
        _cells.ColumnDefinitions.Clear();
        PaintTitle();

        if (_view == ViewMode.Months)
        {
            RenderMonths();
            return;
        }

        if (_view == ViewMode.Years)
        {
            RenderYears();
            return;
        }

        RenderDays();
    }

    private void PaintTitle()
    {
        _titleLabel.Foreground = Theme.Brush(this, Tokens.Ink);
        _titleChevron.Foreground = Theme.Brush(this, Tokens.Muted);
        ApplyFont(_titleLabel, "AppFontFamilyBold");

        bool canDrillUp = _view != ViewMode.Years;
        _titleButton.IsHitTestVisible = canDrillUp;
        _titleChevron.Visibility = canDrillUp ? Visibility.Visible : Visibility.Collapsed;

        if (_view == ViewMode.Years)
        {
            var (start, end) = YearPage(_year);
            _titleLabel.Text = $"{start} – {end}";
            _previous.Content = "سال‌های قبل";
            _next.Content = "سال‌های بعد";
            return;
        }

        if (_view == ViewMode.Months)
        {
            _titleLabel.Text = _year.ToString(CultureInfo.InvariantCulture);
            _previous.Content = "سال قبل";
            _next.Content = "سال بعد";
            return;
        }

        _titleLabel.Text = $"{Dates.ShamsiMonthNames[_month - 1]} {_year}";
        _previous.Content = "ماه قبل";
        _next.Content = "ماه بعد";
    }

    private void RenderDays()
    {
        for (int column = 0; column < 7; column++)
        {
            _cells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        _cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int weekday = 0; weekday < 7; weekday++)
        {
            var label = new TextBlock
            {
                Text = Dates.ShamsiWeekdayNames[weekday][..1],
                Foreground = Theme.Brush(this, Tokens.Muted),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(2),
            };
            ApplyFont(label, "AppFontFamily");
            Grid.SetColumn(label, weekday);
            _cells.Children.Add(label);
        }

        IReadOnlyList<DateOnly> monthDays = Dates.ShamsiMonthDates(_year, _month);
        int offset = Dates.ShamsiWeekIndex(monthDays[0]);
        int rows = 1 + (offset + monthDays.Count - 1) / 7;
        for (int row = 0; row < rows; row++)
        {
            _cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int index = 0; index < monthDays.Count; index++)
        {
            DateOnly day = monthDays[index];
            var (_, _, dayNumber) = Dates.ShamsiYmd(day);
            Button cell = Cell(
                dayNumber.ToString(CultureInfo.InvariantCulture),
                chosen: day == _selected,
                tag: day,
                minHeight: 32,
                OnDayClick,
                holiday: IsHolidayMark(day));
            Grid.SetColumn(cell, (offset + index) % 7);
            Grid.SetRow(cell, 1 + (offset + index) / 7);
            _cells.Children.Add(cell);
        }
    }

    private void RenderMonths()
    {
        FillUniformGrid(3, 4);
        var (selectedYear, selectedMonth, _) = Dates.ShamsiYmd(_selected);
        for (int month = 1; month <= 12; month++)
        {
            Button cell = Cell(
                Dates.ShamsiMonthNames[month - 1],
                chosen: selectedYear == _year && selectedMonth == month,
                tag: month,
                minHeight: 48,
                OnMonthClick);
            PlaceUniform(cell, month - 1, 3);
            _cells.Children.Add(cell);
        }
    }

    private void RenderYears()
    {
        FillUniformGrid(3, 4);
        var (start, _) = YearPage(_year);
        int selectedYear = Dates.ShamsiYmd(_selected).Year;
        for (int index = 0; index < YearSpan; index++)
        {
            int year = start + index;
            Button cell = Cell(
                year.ToString(CultureInfo.InvariantCulture),
                chosen: year == selectedYear,
                tag: year,
                minHeight: 48,
                OnYearClick);
            PlaceUniform(cell, index, 3);
            _cells.Children.Add(cell);
        }
    }

    private void FillUniformGrid(int columns, int rows)
    {
        for (int column = 0; column < columns; column++)
        {
            _cells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (int row = 0; row < rows; row++)
        {
            _cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }

    private static void PlaceUniform(FrameworkElement element, int index, int columns)
    {
        Grid.SetColumn(element, index % columns);
        Grid.SetRow(element, index / columns);
    }

    private bool IsHolidayMark(DateOnly day)
    {
        return Dates.ShamsiWeekIndex(day) == FridayWeekday
            || OfficialDates.Contains(Dates.StorageDate(day));
    }

    private Button Cell(string text, bool chosen, object tag, int minHeight, RoutedEventHandler click, bool holiday = false)
    {
        var cell = new Button
        {
            Content = text,
            Background = chosen ? Theme.Brush(this, Tokens.Accent) : Theme.Brush(this, Tokens.Surface),
            Foreground = chosen
                ? Theme.Brush(this, Tokens.OnAccent)
                : Theme.Brush(this, holiday ? Tokens.Danger : Tokens.Ink),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0, 4, 0),
            MinWidth = 32,
            MinHeight = minHeight,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Tag = tag,
        };
        ThemeLooks.Apply(cell, "AppChromeButton");
        ApplyFont(cell, "AppFontFamily");
        cell.Click += click;
        return cell;
    }

    private static IReadOnlySet<string> EmptyOfficialDates()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }

    private static (int Start, int End) YearPage(int year)
    {
        int start = Math.Max(1, ((year - 1) / YearSpan) * YearSpan + 1);
        return (start, start + YearSpan - 1);
    }

    private static void ApplyFont(Control control, string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out object? value) == true &&
            value is FontFamily font)
        {
            control.FontFamily = font;
        }
    }

    private static void ApplyFont(TextBlock block, string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out object? value) == true &&
            value is FontFamily font)
        {
            block.FontFamily = font;
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

    private void OnMonthClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: int month })
        {
            _month = month;
            if (PickMonth)
            {
                MonthPicked?.Invoke(this, (_year, month));
                return;
            }

            _view = ViewMode.Days;
            Render();
        }
    }

    private void OnYearClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: int year })
        {
            _year = year;
            _view = ViewMode.Months;
            Render();
        }
    }
}
