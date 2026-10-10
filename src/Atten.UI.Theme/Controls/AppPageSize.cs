using System.Globalization;
using Atten.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.UI.Theme;

/// <summary>
/// How many rows each page shows. Sizes come from Core.Paging.
/// </summary>
public sealed class AppPageSize : UserControl
{
    private readonly AppComboBox _combo = new();
    private bool _syncing;
    private int _size = Paging.DefaultPageSize;

    public AppPageSize()
    {
        FlowDirection = FlowDirection.RightToLeft;

        var label = new TextBlock
        {
            Text = "در هر صفحه",
            VerticalAlignment = VerticalAlignment.Center,
        };

        _combo.MinWidth = 88;
        _combo.VerticalAlignment = VerticalAlignment.Center;
        foreach (int size in Paging.PageSizes)
        {
            _combo.Items.Add(size.ToString(CultureInfo.InvariantCulture));
        }

        _combo.SelectionChanged += OnSelectionChanged;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(label);
        row.Children.Add(_combo);
        Content = row;

        Loaded += (_, _) =>
        {
            ApplyCaptionStyle(label);
            SyncCombo();
        };
    }

    public event EventHandler? PageSizeChanged;

    public int SelectedSize
    {
        get => _size;
        set
        {
            int next = Paging.NormalizeSize(value);
            if (next == _size && _combo.SelectedIndex >= 0)
            {
                return;
            }

            _size = next;
            SyncCombo();
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_syncing || _combo.SelectedIndex < 0)
        {
            return;
        }

        int next = Paging.PageSizes[_combo.SelectedIndex];
        if (next == _size)
        {
            return;
        }

        _size = next;
        PageSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncCombo()
    {
        int index = IndexOfSize(_size);
        _syncing = true;
        _combo.SelectedIndex = index < 0 ? IndexOfSize(Paging.DefaultPageSize) : index;
        _syncing = false;
    }

    private static int IndexOfSize(int size)
    {
        for (int index = 0; index < Paging.PageSizes.Count; index++)
        {
            if (Paging.PageSizes[index] == size)
            {
                return index;
            }
        }

        return -1;
    }

    private static void ApplyCaptionStyle(TextBlock label)
    {
        if (Application.Current?.Resources.TryGetValue("AppCaptionText", out object? value) == true
            && value is Style style)
        {
            label.Style = style;
        }
    }
}
