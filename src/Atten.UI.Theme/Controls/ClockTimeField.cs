using Atten.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Atten.UI.Theme;

/// <summary>
/// Clock on screen as HH:MM:SS. Colons come from Core. No sheet rules here.
/// </summary>
public sealed class ClockTimeField : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(ClockTimeField),
        new PropertyMetadata(string.Empty, OnTextChanged));

    private readonly Border _fieldBorder;
    private readonly TextBox _timeText;
    private readonly Button _confirm;
    private readonly Button _shiftRight;
    private readonly Button _shiftLeft;
    private readonly Button _cancel;
    private readonly FontIcon _confirmIcon;
    private readonly FontIcon _shiftRightIcon;
    private readonly FontIcon _shiftLeftIcon;
    private readonly FontIcon _cancelIcon;
    private bool _syncing;
    private bool _accepting;
    private string _lastText = string.Empty;
    private string _lastDigits = string.Empty;

    public ClockTimeField()
    {
        FlowDirection = FlowDirection.RightToLeft;
        MinWidth = 208;

        _timeText = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            PlaceholderText = "۰۰:۰۰:۰۰",
            MinWidth = 132,
            FlowDirection = FlowDirection.LeftToRight,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ThemeLooks.Apply(_timeText, "AppTextBoxNoClearStyle");

        _fieldBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = _timeText,
        };

        _confirmIcon = ActionIcon("\uE73E");
        _shiftRightIcon = ActionIcon("\uE76C");
        _shiftRightIcon.FlowDirection = FlowDirection.LeftToRight;
        _shiftLeftIcon = ActionIcon("\uE76B");
        _shiftLeftIcon.FlowDirection = FlowDirection.LeftToRight;
        _cancelIcon = ActionIcon("\uE711");
        _confirm = ActionButton(_confirmIcon);
        _shiftRight = ActionButton(_shiftRightIcon);
        _shiftLeft = ActionButton(_shiftLeftIcon);
        _cancel = ActionButton(_cancelIcon);
        ToolTipService.SetToolTip(_shiftRight, "شیفت به راست");
        ToolTipService.SetToolTip(_shiftLeft, "شیفت به چپ");
        _confirm.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => _accepting = true), handledEventsToo: true);
        _shiftRight.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => _accepting = true), handledEventsToo: true);
        _shiftLeft.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => _accepting = true), handledEventsToo: true);
        _confirm.Click += (_, _) =>
        {
            _accepting = true;
            Confirmed?.Invoke(this, EventArgs.Empty);
        };
        _shiftRight.Click += (_, _) =>
        {
            _accepting = true;
            ShiftedRight?.Invoke(this, EventArgs.Empty);
        };
        _shiftLeft.Click += (_, _) =>
        {
            _accepting = true;
            ShiftedLeft?.Invoke(this, EventArgs.Empty);
        };
        _cancel.Click += (_, _) => Cancelled?.Invoke(this, EventArgs.Empty);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
        };
        actions.Children.Add(_confirm);
        actions.Children.Add(_shiftRight);
        actions.Children.Add(_shiftLeft);
        actions.Children.Add(_cancel);

        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(_fieldBorder);
        body.Children.Add(actions);
        Content = body;

        _timeText.TextChanged += OnTimeTextChanged;
        _timeText.KeyDown += OnTimeKeyDown;
        Loaded += (_, _) =>
        {
            PaintChrome();
        };
        ActualThemeChanged += (_, _) =>
        {
            PaintChrome();
        };
        _timeText.GotFocus += (_, _) => _fieldBorder.BorderBrush = Theme.Brush(this, Tokens.Accent);
        _timeText.LostFocus += (_, _) => _fieldBorder.BorderBrush = Theme.Brush(this, Tokens.Line);
        LostFocus += OnLostFocus;
    }

    public event EventHandler? Confirmed;

    public event EventHandler? ShiftedRight;

    public event EventHandler? ShiftedLeft;

    public event EventHandler? Cancelled;

    /// <summary>Clock text shown and typed in the field, with colons.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Typed clock without a trailing colon, ready to parse.</summary>
    public string CommitText => (Text ?? string.Empty).Trim().TrimEnd(':');

    public void FocusInput()
    {
        _timeText.Focus(FocusState.Programmatic);
        _timeText.SelectAll();
    }

    private void PaintChrome()
    {
        _fieldBorder.Background = Theme.Brush(this, Tokens.Surface);
        _fieldBorder.BorderBrush = Theme.Brush(this, Tokens.Line);
        PaintAction(_confirm, _confirmIcon, Tokens.Success);
        PaintAction(_shiftRight, _shiftRightIcon, Tokens.Accent);
        PaintAction(_shiftLeft, _shiftLeftIcon, Tokens.Accent);
        PaintAction(_cancel, _cancelIcon, Tokens.Danger);
    }

    private void PaintAction(Button button, FontIcon icon, string token)
    {
        ThemeLooks.Apply(button, "AppChromeButton");
        button.Background = Theme.Brush(this, Tokens.Surface);
        button.BorderBrush = Theme.Brush(this, Tokens.Line);
        icon.Foreground = Theme.Brush(this, token);
    }

    private static FontIcon ActionIcon(string glyph)
    {
        return new FontIcon
        {
            FontFamily = Theme.IconFontFamily,
            FontSize = 14,
            Glyph = glyph,
        };
    }

    private static Button ActionButton(FontIcon icon)
    {
        return new Button
        {
            Content = icon,
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            CornerRadius = new CornerRadius(4),
        };
    }

    private void OnLostFocus(object sender, RoutedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_accepting || ContainsFocus())
            {
                return;
            }

            Cancelled?.Invoke(this, EventArgs.Empty);
        });
    }

    private bool ContainsFocus()
    {
        if (XamlRoot is null)
        {
            return false;
        }

        DependencyObject? current = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (current is not null)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void OnTimeKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            Confirmed?.Invoke(this, EventArgs.Empty);
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Escape)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            args.Handled = true;
        }
    }

    private void OnTimeTextChanged(object sender, TextChangedEventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        ApplyTypedText(_timeText.Text ?? string.Empty, _timeText.SelectionStart);
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var field = (ClockTimeField)sender;
        if (field._syncing)
        {
            return;
        }

        field.ApplyTypedText(args.NewValue as string ?? string.Empty, caret: null);
    }

    private void ApplyTypedText(string raw, int? caret)
    {
        int digitsBefore = DigitsBefore(raw, caret ?? raw.Length);
        string digits = Dates.ClockInputDigits(raw);
        if (digits.Length == _lastDigits.Length && raw.Length < _lastText.Length && digits.Length > 0)
        {
            int removeAt = Math.Clamp(digitsBefore - 1, 0, digits.Length - 1);
            digits = digits.Remove(removeAt, 1);
            digitsBefore = removeAt;
        }

        digitsBefore = Math.Clamp(digitsBefore, 0, digits.Length);
        string formatted = Dates.FormatClockInput(digits);
        int nextCaret = CaretAfterDigits(formatted, digitsBefore);

        _syncing = true;
        if (_timeText.Text != formatted)
        {
            _timeText.Text = formatted;
        }

        _timeText.SelectionStart = nextCaret;
        _timeText.SelectionLength = 0;
        SetValue(TextProperty, formatted);
        Remember(formatted);
        _syncing = false;
    }

    private void Remember(string formatted)
    {
        _lastText = formatted;
        _lastDigits = Dates.ClockInputDigits(formatted);
    }

    private static int DigitsBefore(string text, int caret)
    {
        int limit = Math.Clamp(caret, 0, text.Length);
        return Dates.ClockInputDigits(text[..limit]).Length;
    }

    private static int CaretAfterDigits(string formatted, int digitCount)
    {
        if (digitCount <= 0)
        {
            return 0;
        }

        int seen = 0;
        for (int i = 0; i < formatted.Length; i++)
        {
            if (formatted[i] is < '0' or > '9')
            {
                continue;
            }

            seen += 1;
            if (seen != digitCount)
            {
                continue;
            }

            int index = i + 1;
            if (index < formatted.Length && formatted[index] == ':')
            {
                index += 1;
            }

            return index;
        }

        return formatted.Length;
    }
}
