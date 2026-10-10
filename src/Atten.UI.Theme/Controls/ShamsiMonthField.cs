using Atten.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Shamsi year and month only. Empty means no filter. Calendar math stays in Core.
/// </summary>
public sealed class ShamsiMonthField : UserControl
{
    public const string AllCaption = "همه ماه‌ها";

    private readonly Border _fieldBorder;
    private readonly TextBlock _caption = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly FontIcon _chevron = new()
    {
        FontFamily = new FontFamily("Segoe Fluent Icons"),
        FontSize = 12,
        Glyph = "\uE70D",
        Margin = new Thickness(8, 0, 0, 0),
    };
    private readonly Button _clear;
    private readonly ShamsiCalendar _calendar = new();
    private readonly AppPopup _popup = new();
    private int? _year;
    private int? _month;

    public ShamsiMonthField()
    {
        FlowDirection = FlowDirection.RightToLeft;
        MinWidth = 168;
        _calendar.PickMonth = true;
        _popup.Content = _calendar;
        _calendar.MonthPicked += OnMonthPicked;

        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.Children.Add(_caption);
        title.Children.Add(_chevron);

        var open = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            Content = title,
            Flyout = _popup,
        };
        ThemeLooks.Apply(open, "AppChromeButton");
        _popup.Opening += OnCalendarOpening;

        _clear = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            VerticalAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed,
            Content = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12,
                Glyph = "\uE711",
            },
        };
        ThemeLooks.Apply(_clear, "AppChromeButton");
        _clear.Click += (_, _) => Clear();

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(open, 0);
        Grid.SetColumn(_clear, 1);
        row.Children.Add(open);
        row.Children.Add(_clear);

        _fieldBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = row,
        };
        Content = _fieldBorder;

        Loaded += (_, _) =>
        {
            Paint();
            ((FontIcon)_clear.Content).Foreground = Theme.Brush(this, Tokens.Muted);
        };
        ActualThemeChanged += (_, _) => Paint();
    }

    public event EventHandler? MonthChanged;

    public int? Year => _year;

    public int? Month => _month;

    public void SetMonth(int year, int month)
    {
        _year = year;
        _month = month;
        if (IsLoaded)
        {
            Paint();
            return;
        }

        _caption.Text = Dates.FormatShamsiMonth(year, month);
    }

    public void Clear()
    {
        if (_year is null && _month is null)
        {
            return;
        }

        _year = null;
        _month = null;
        Paint();
        MonthChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCalendarOpening(object? sender, object args)
    {
        DateOnly shown = DateOnly.FromDateTime(DateTime.Today);
        if (_year is int year && _month is int month)
        {
            shown = Dates.ShamsiMonthSpan(year, month).Start;
        }

        _calendar.Show(shown);
    }

    private void OnMonthPicked(object? sender, (int Year, int Month) value)
    {
        _year = value.Year;
        _month = value.Month;
        Paint();
        _popup.Hide();
        MonthChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Paint()
    {
        _fieldBorder.Background = Theme.Brush(this, Tokens.Surface);
        _fieldBorder.BorderBrush = Theme.Brush(this, Tokens.Line);
        if (_year is int year && _month is int month)
        {
            _caption.Text = Dates.FormatShamsiMonth(year, month);
            _caption.Foreground = Theme.Brush(this, Tokens.Ink);
            _clear.Visibility = Visibility.Visible;
        }
        else
        {
            _caption.Text = AllCaption;
            _caption.Foreground = Theme.Brush(this, Tokens.Muted);
            _clear.Visibility = Visibility.Collapsed;
        }

        _chevron.Foreground = Theme.Brush(this, Tokens.Muted);
        if (Application.Current?.Resources.TryGetValue("AppFontFamily", out object? value) == true
            && value is FontFamily font)
        {
            _caption.FontFamily = font;
        }
    }
}
