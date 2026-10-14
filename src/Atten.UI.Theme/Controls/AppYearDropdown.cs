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

    private readonly AppDropdownButton _dropdown = new();
    private readonly StackPanel _list = new();
    private readonly Dictionary<int, CheckBox> _checks = [];
    private readonly HashSet<int> _selected = [];
    private IReadOnlyList<int> _years = [];
    private bool _syncing;

    public AppYearDropdown()
    {
        FlowDirection = FlowDirection.RightToLeft;
        HorizontalAlignment = HorizontalAlignment.Right;
        _dropdown.Caption = DefaultCaption;
        _dropdown.Menu = _list;
        _dropdown.Opening += (_, _) => Rebuild();
        Content = _dropdown;

        Loaded += (_, _) => UpdateCaption();
        ActualThemeChanged += (_, _) =>
        {
            foreach (CheckBox box in _checks.Values)
            {
                PaintCheck(box);
            }
        };
    }

    public event EventHandler? SelectionChanged;

    public IReadOnlyList<int> Years => _years;

    public IReadOnlySet<int> SelectedYears => _selected;

    public string CaptionText => _dropdown.Caption;

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
            var box = new AppCheckBox
            {
                Content = year.ToString(CultureInfo.InvariantCulture),
                IsChecked = _selected.Contains(year),
                Tag = year,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            PaintCheck(box);
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
        _dropdown.Caption = FormatCaption(_selected);
    }

    private void PaintCheck(CheckBox box)
    {
        Brush fill = Theme.Brush(box, Tokens.Accent);
        Brush glyph = Theme.Brush(box, Tokens.OnAccent);
        box.Resources["CheckBoxCheckBackgroundFillChecked"] = fill;
        box.Resources["CheckBoxCheckBackgroundFillCheckedPointerOver"] = fill;
        box.Resources["CheckBoxCheckBackgroundFillCheckedPressed"] = fill;
        box.Resources["CheckBoxCheckBackgroundStrokeChecked"] = fill;
        box.Resources["CheckBoxCheckBackgroundStrokeCheckedPointerOver"] = fill;
        box.Resources["CheckBoxCheckBackgroundStrokeCheckedPressed"] = fill;
        box.Resources["CheckBoxCheckGlyphForegroundChecked"] = glyph;
        box.Resources["CheckBoxCheckGlyphForegroundCheckedPointerOver"] = glyph;
        box.Resources["CheckBoxCheckGlyphForegroundCheckedPressed"] = glyph;
    }
}
