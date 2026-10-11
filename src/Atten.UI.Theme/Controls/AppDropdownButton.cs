using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

/// <summary>
/// Secondary button with a caption and a chevron that flips when the popup is open.
/// Year pickers, bulk actions, and later dropdowns share this chrome.
/// </summary>
public sealed class AppDropdownButton : UserControl
{
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption),
        typeof(string),
        typeof(AppDropdownButton),
        new PropertyMetadata(string.Empty, OnCaptionChanged));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(AppButtonKind),
        typeof(AppDropdownButton),
        new PropertyMetadata(AppButtonKind.Secondary, OnKindChanged));

    private readonly AppDropdownCaption _caption = new();
    private readonly AppButton _button;
    private readonly AppPopup _popup = new();

    public AppDropdownButton()
    {
        FlowDirection = FlowDirection.RightToLeft;
        _button = new AppButton
        {
            Kind = AppButtonKind.Secondary,
            Content = _caption,
            Flyout = _popup,
        };
        Content = _button;
        _popup.Opening += OnPopupOpening;
        _popup.Closed += OnPopupClosed;
    }

    public event EventHandler? Opening;

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value ?? string.Empty);
    }

    public AppButtonKind Kind
    {
        get => (AppButtonKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public UIElement? Menu
    {
        get => _popup.Content as UIElement;
        set => _popup.Content = value;
    }

    public bool IsOpen => _caption.IsOpen;

    public void Hide()
    {
        _popup.Hide();
    }

    private void OnPopupOpening(object? sender, object args)
    {
        _caption.IsOpen = true;
        FitMenuToButton();
        Opening?.Invoke(this, EventArgs.Empty);
    }

    private void FitMenuToButton()
    {
        double width = Math.Max(_button.ActualWidth, ActualWidth);
        _popup.MinPresenterWidth = width;
        if (width > 0 && _popup.Content is FrameworkElement menu)
        {
            menu.MinWidth = width;
            menu.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }

    private void OnPopupClosed(object? sender, object args)
    {
        _caption.IsOpen = false;
    }

    private static void OnCaptionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppDropdownButton)sender)._caption.Text = args.NewValue as string ?? string.Empty;
    }

    private static void OnKindChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppDropdownButton)sender)._button.Kind = (AppButtonKind)args.NewValue;
    }
}
