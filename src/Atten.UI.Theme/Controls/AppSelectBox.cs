using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Row or header checkbox for multi-select lists. Screens keep the selected keys.
/// </summary>
public sealed class AppSelectBox : UserControl
{
    public static readonly DependencyProperty IsChosenProperty = DependencyProperty.Register(
        nameof(IsChosen),
        typeof(bool),
        typeof(AppSelectBox),
        new PropertyMetadata(false, OnIsChosenChanged));

    public static readonly DependencyProperty IsThreeStateProperty = DependencyProperty.Register(
        nameof(IsThreeState),
        typeof(bool),
        typeof(AppSelectBox),
        new PropertyMetadata(false, OnIsThreeStateChanged));

    private readonly AppCheckBox _box = new();
    private bool _syncing;

    public AppSelectBox()
    {
        MinWidth = 0;
        MinHeight = 0;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
        _box.MinWidth = 0;
        _box.MinHeight = 0;
        _box.Padding = new Thickness(0);
        _box.HorizontalAlignment = HorizontalAlignment.Center;
        _box.VerticalAlignment = VerticalAlignment.Center;
        _box.HorizontalContentAlignment = HorizontalAlignment.Center;
        _box.Checked += OnBoxChanged;
        _box.Unchecked += OnBoxChanged;
        _box.Indeterminate += OnBoxChanged;
        Content = _box;
        Loaded += OnLoaded;
        ActualThemeChanged += (_, _) => PaintCheck();
    }

    public event EventHandler? ChosenChanged;

    public bool IsChosen
    {
        get => (bool)GetValue(IsChosenProperty);
        set => SetValue(IsChosenProperty, value);
    }

    public bool IsThreeState
    {
        get => (bool)GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    public bool? CheckState
    {
        get => _box.IsChecked;
        set
        {
            _syncing = true;
            _box.IsThreeState = IsThreeState || value is null;
            _box.IsChecked = value;
            if (value is bool chosen)
            {
                IsChosen = chosen;
            }

            _syncing = false;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ThemeLooks.Apply(_box, "AppCheckBox");
        _box.MinWidth = 0;
        _box.MinHeight = 0;
        _box.Padding = new Thickness(0);
        _box.HorizontalAlignment = HorizontalAlignment.Center;
        _box.HorizontalContentAlignment = HorizontalAlignment.Center;
        PaintCheck();
        SyncBox();
    }

    private void PaintCheck()
    {
        Brush fill = Theme.Brush(this, Tokens.Accent);
        Brush glyph = Theme.Brush(this, Tokens.OnAccent);
        Brush surface = Theme.Brush(this, Tokens.Surface);
        Brush line = Theme.Brush(this, Tokens.Line);
        foreach (string key in new[]
        {
            "CheckBoxCheckBackgroundFillUnchecked",
            "CheckBoxCheckBackgroundFillUncheckedPointerOver",
            "CheckBoxCheckBackgroundFillUncheckedPressed",
            "CheckBoxCheckBackgroundFillUncheckedDisabled",
        })
        {
            _box.Resources[key] = surface;
        }

        foreach (string key in new[]
        {
            "CheckBoxCheckBackgroundStrokeUnchecked",
            "CheckBoxCheckBackgroundStrokeUncheckedPointerOver",
            "CheckBoxCheckBackgroundStrokeUncheckedPressed",
            "CheckBoxCheckBackgroundStrokeUncheckedDisabled",
        })
        {
            _box.Resources[key] = line;
        }

        foreach (string key in new[]
        {
            "CheckBoxCheckBackgroundFillChecked",
            "CheckBoxCheckBackgroundFillCheckedPointerOver",
            "CheckBoxCheckBackgroundFillCheckedPressed",
            "CheckBoxCheckBackgroundStrokeChecked",
            "CheckBoxCheckBackgroundStrokeCheckedPointerOver",
            "CheckBoxCheckBackgroundStrokeCheckedPressed",
            "CheckBoxCheckBackgroundFillIndeterminate",
            "CheckBoxCheckBackgroundFillIndeterminatePointerOver",
            "CheckBoxCheckBackgroundFillIndeterminatePressed",
            "CheckBoxCheckBackgroundStrokeIndeterminate",
            "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver",
            "CheckBoxCheckBackgroundStrokeIndeterminatePressed",
        })
        {
            _box.Resources[key] = fill;
        }

        foreach (string key in new[]
        {
            "CheckBoxCheckGlyphForegroundChecked",
            "CheckBoxCheckGlyphForegroundCheckedPointerOver",
            "CheckBoxCheckGlyphForegroundCheckedPressed",
            "CheckBoxCheckGlyphForegroundIndeterminate",
            "CheckBoxCheckGlyphForegroundIndeterminatePointerOver",
            "CheckBoxCheckGlyphForegroundIndeterminatePressed",
        })
        {
            _box.Resources[key] = glyph;
        }
    }

    private static void OnIsChosenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((AppSelectBox)sender).SyncBox();
    }

    private static void OnIsThreeStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var box = (AppSelectBox)sender;
        box._box.IsThreeState = box.IsThreeState;
    }

    private void OnBoxChanged(object sender, RoutedEventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        if (_box.IsChecked is not bool chosen)
        {
            return;
        }

        if (IsChosen == chosen)
        {
            ChosenChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsChosen = chosen;
        ChosenChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncBox()
    {
        if (_box.IsChecked == IsChosen)
        {
            return;
        }

        _syncing = true;
        _box.IsChecked = IsChosen;
        _syncing = false;
    }
}
