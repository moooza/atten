using System.Globalization;
using Atten.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Shamsi on screen. Gregorian DateOnly / YYYY-MM-DD for the rest of the app.
/// Calendar math stays in Core.
/// </summary>
public sealed class ShamsiDateField : UserControl
{
    public static readonly DependencyProperty StorageDateProperty = DependencyProperty.Register(
        nameof(StorageDate),
        typeof(string),
        typeof(ShamsiDateField),
        new PropertyMetadata(null, OnStorageDateChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(ShamsiDateField),
        new PropertyMetadata(string.Empty, OnTextChanged));

    private readonly Border _fieldBorder;
    private readonly AppTextBox _dateText;
    private readonly ShamsiCalendar _calendar = new();
    private readonly AppPopup _popup = new();
    private bool _syncing;

    public ShamsiDateField()
    {
        FlowDirection = FlowDirection.RightToLeft;

        _dateText = new AppTextBox
        {
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            PlaceholderText = "۰۰۰۰/۰۰/۰۰",
            MinWidth = 140,
        };

        var calendarButton = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 14,
                Glyph = "\uE787",
            },
        };

        _popup.Content = _calendar;
        calendarButton.Flyout = _popup;
        _popup.Opening += OnCalendarOpening;
        _calendar.DatePicked += OnDatePicked;
        _dateText.TextChanged += OnDateTextChanged;

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_dateText, 0);
        Grid.SetColumn(calendarButton, 1);
        row.Children.Add(_dateText);
        row.Children.Add(calendarButton);

        _fieldBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = row,
        };
        Content = _fieldBorder;

        Loaded += (_, _) =>
        {
            PaintChrome();
            ((FontIcon)calendarButton.Content).Foreground = Theme.Brush(this, Tokens.Accent);
        };
        ActualThemeChanged += (_, _) =>
        {
            PaintChrome();
            ((FontIcon)calendarButton.Content).Foreground = Theme.Brush(this, Tokens.Accent);
        };
        _dateText.GotFocus += (_, _) => _fieldBorder.BorderBrush = Theme.Brush(this, Tokens.Accent);
        _dateText.LostFocus += (_, _) => _fieldBorder.BorderBrush = Theme.Brush(this, Tokens.Line);
    }

    public event EventHandler? DateChanged;

    /// <summary>Gregorian storage text YYYY-MM-DD, or null when empty/invalid.</summary>
    public string? StorageDate
    {
        get => (string?)GetValue(StorageDateProperty);
        set => SetValue(StorageDateProperty, value);
    }

    /// <summary>Shamsi text shown and typed in the field.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public DateOnly? GregorianDate
    {
        get => TryParseGregorian(StorageDate) ?? TryParseShamsi(Text);
        set => StorageDate = value is { } day ? Dates.StorageDate(day) : null;
    }

    private void PaintChrome()
    {
        _fieldBorder.Background = Theme.Brush(this, Tokens.Surface);
        _fieldBorder.BorderBrush = Theme.Brush(this, Tokens.Line);
    }

    private void OnCalendarOpening(object? sender, object args)
    {
        _calendar.Show(GregorianDate ?? DateOnly.FromDateTime(DateTime.Today));
    }

    private void OnDatePicked(object? sender, DateOnly day)
    {
        GregorianDate = day;
        _popup.Hide();
        DateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDateTextChanged(object sender, TextChangedEventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        SetValue(TextProperty, _dateText.Text);
        DateOnly? parsed = TryParseShamsi(_dateText.Text);
        _syncing = true;
        StorageDate = parsed is { } day ? Dates.StorageDate(day) : null;
        _syncing = false;
        DateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnStorageDateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var field = (ShamsiDateField)sender;
        if (field._syncing)
        {
            return;
        }

        field._syncing = true;
        DateOnly? gregorian = TryParseGregorian(args.NewValue as string);
        string shamsi = gregorian is { } day ? Dates.FormatShamsiDate(day) : string.Empty;
        field.SetValue(TextProperty, shamsi);
        field._dateText.Text = shamsi;
        field._syncing = false;
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var field = (ShamsiDateField)sender;
        if (field._syncing)
        {
            return;
        }

        string text = args.NewValue as string ?? string.Empty;
        if (field._dateText.Text != text)
        {
            field._dateText.Text = text;
        }
    }

    private static DateOnly? TryParseShamsi(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return Dates.ParseShamsiDate(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static DateOnly? TryParseGregorian(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateOnly.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly day))
        {
            return day;
        }

        return null;
    }
}
