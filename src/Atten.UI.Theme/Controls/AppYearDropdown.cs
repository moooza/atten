using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Atten.UI.Theme;

/// <summary>
/// Themed year picker with checkboxes in a chrome-less popup.
/// Screens pass the year list; this control does not compute cooperation years.
/// </summary>
public sealed class AppYearDropdown : UserControl
{
    public const string DefaultCaption = "سال";

    private readonly AppButton _button;
    private readonly TextBlock _caption = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly FontIcon _chevron = new()
    {
        FontFamily = new FontFamily("Segoe Fluent Icons"),
        FontSize = 14,
        Glyph = "\uE70D",
        Margin = new Thickness(8, 0, 0, 0),
    };
    private readonly AppPopup _popup = new();
    private readonly StackPanel _list = new();
    private readonly Dictionary<int, CheckBox> _checks = [];
    private readonly HashSet<int> _selected = [];
    private IReadOnlyList<int> _years = [];
    private bool _syncing;

    public AppYearDropdown()
    {
        FlowDirection = FlowDirection.RightToLeft;
        HorizontalAlignment = HorizontalAlignment.Right;

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_caption);
        row.Children.Add(_chevron);

        _popup.Content = _list;
        _button = new AppButton
        {
            Kind = AppButtonKind.Secondary,
            Content = row,
            Flyout = _popup,
        };
        Content = _button;

        Loaded += (_, _) =>
        {
            UpdateCaption();
            Tint();
        };
        ActualThemeChanged += (_, _) => Tint();
        _popup.Opening += (_, _) => Rebuild();
    }

    public event EventHandler? SelectionChanged;

    public IReadOnlyList<int> Years => _years;

    public IReadOnlySet<int> SelectedYears => _selected;

    public string CaptionText => _caption.Text;

    public void Bind(IEnumerable<int> years, IEnumerable<int> selected)
    {
        ArgumentNullException.ThrowIfNull(years);
        ArgumentNullException.ThrowIfNull(selected);

        _years = years.ToList();
        _selected.Clear();
        foreach (int year in selected)
        {
            _selected.Add(year);
        }

        Rebuild();
        UpdateCaption();
    }

    public static string FormatCaption(IEnumerable<int> years)
    {
        ArgumentNullException.ThrowIfNull(years);
        int[] shown = years.Distinct().OrderByDescending(year => year).ToArray();
        if (shown.Length == 0)
        {
            return DefaultCaption;
        }

        return $"{DefaultCaption} {string.Join("، ", shown)}";
    }

    private void Rebuild()
    {
        _syncing = true;
        _list.Children.Clear();
        _checks.Clear();
        foreach (int year in _years)
        {
            var box = new CheckBox
            {
                Content = year.ToString(CultureInfo.InvariantCulture),
                IsChecked = _selected.Contains(year),
                Tag = year,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            ThemeLooks.Apply(box, "AppCheckBox");
            box.Checked += OnYearToggled;
            box.Unchecked += OnYearToggled;
            _checks[year] = box;
            _list.Children.Add(box);
        }

        _syncing = false;
    }

    private void OnYearToggled(object sender, RoutedEventArgs args)
    {
        if (_syncing || sender is not CheckBox { Tag: int year } box)
        {
            return;
        }

        if (box.IsChecked == true)
        {
            _selected.Add(year);
        }
        else
        {
            _selected.Remove(year);
        }

        UpdateCaption();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCaption()
    {
        _caption.Text = FormatCaption(_selected);
    }

    private void Tint()
    {
        Brush ink = Theme.Brush(this, Tokens.Ink);
        _caption.Foreground = ink;
        _chevron.Foreground = ink;
        if (Application.Current?.Resources.TryGetValue("AppFontFamily", out object? family) == true
            && family is FontFamily font)
        {
            _caption.FontFamily = font;
        }

        if (Application.Current?.Resources.TryGetValue("BodyFontSize", out object? size) == true
            && size is double points)
        {
            _caption.FontSize = points;
        }
    }
}
