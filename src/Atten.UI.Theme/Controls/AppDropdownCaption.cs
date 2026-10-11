using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Label plus open/close chevron for a dropdown button.
/// </summary>
public sealed class AppDropdownCaption : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(AppDropdownCaption),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(AppDropdownCaption),
        new PropertyMetadata(false, OnIsOpenChanged));

    private readonly TextBlock _label = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly AppChevron _chevron = new();

    public AppDropdownCaption()
    {
        FlowDirection = FlowDirection.RightToLeft;
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
        };
        row.Children.Add(_label);
        row.Children.Add(_chevron);
        Content = row;
        Loaded += (_, _) => Tint();
        ActualThemeChanged += (_, _) => Tint();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value ?? string.Empty);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppDropdownCaption)sender)._label.Text = args.NewValue as string ?? string.Empty;
    }

    private static void OnIsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppDropdownCaption)sender)._chevron.IsOpen = (bool)args.NewValue;
    }

    private void Tint()
    {
        _label.Foreground = Theme.Brush(this, Tokens.Ink);
        if (Application.Current?.Resources.TryGetValue("AppFontFamily", out object? family) == true
            && family is FontFamily font)
        {
            _label.FontFamily = font;
        }

        if (Application.Current?.Resources.TryGetValue("BodyFontSize", out object? size) == true
            && size is double points)
        {
            _label.FontSize = points;
        }
    }
}
