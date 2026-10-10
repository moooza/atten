using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

public sealed class AppNavButton : Button
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(AppNavButton),
        new PropertyMetadata("\uE10F", OnContentChanged));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption),
        typeof(string),
        typeof(AppNavButton),
        new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(AppNavButton),
        new PropertyMetadata(false, OnSelectedChanged));

    private readonly FontIcon _icon = new()
    {
        FontFamily = new FontFamily("Segoe Fluent Icons"),
        FontSize = 18,
    };

    private readonly TextBlock _caption = new()
    {
        Margin = new Thickness(8, 0, 6, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    public AppNavButton()
    {
        FlowDirection = FlowDirection.RightToLeft;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        row.Children.Add(_icon);
        row.Children.Add(_caption);
        Content = row;
        Loaded += OnLoaded;
        ActualThemeChanged += (_, _) => TintContent();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (Application.Current?.Resources.TryGetValue("BodyFontSize", out object? size) == true &&
            size is double points)
        {
            _caption.FontSize = points;
        }

        if (Application.Current?.Resources.TryGetValue("AppFontFamily", out object? family) == true &&
            family is FontFamily font)
        {
            _caption.FontFamily = font;
        }

        SyncContent();
        ApplySelectedStyle();
    }

    private static void OnContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppNavButton)sender).SyncContent();
    }

    private static void OnSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppNavButton)sender).ApplySelectedStyle();
    }

    private void SyncContent()
    {
        _icon.Glyph = Glyph;
        _caption.Text = Caption;
    }

    private void ApplySelectedStyle()
    {
        ThemeLooks.Apply(this, IsSelected ? "AppNavButtonSelected" : "AppNavButton");
        TintContent();
    }

    private void TintContent()
    {
        _icon.Foreground = Foreground;
        _caption.Foreground = Foreground;
    }
}
