using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Atten.App.Calculation;

internal sealed class SheetDayRowView : UserControl
{
    private readonly SheetDayRow _row;
    private readonly int _punchCount;
    private readonly SheetDayCallbacks _callbacks;
    private readonly Border _frame = new();
    private readonly Grid _grid = new();
    private AppTextBox? _editor;
    private int _editSlot = -1;
    private bool _committing;

    public SheetDayRowView(SheetDayRow row, int punchCount, SheetDayCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(callbacks);
        _row = row;
        _punchCount = punchCount;
        _callbacks = callbacks;
        FlowDirection = FlowDirection.RightToLeft;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        _frame.Child = _grid;
        Content = _frame;
        Loaded += (_, _) => Build();
        ActualThemeChanged += (_, _) =>
        {
            CloseEditor();
            _grid.Children.Clear();
            Build();
        };
    }

    public bool CommitEdit()
    {
        if (_editor is null || _editSlot < 0 || _committing)
        {
            return false;
        }

        _committing = true;
        string text = _editor.Text;
        int slot = _editSlot;
        CloseEditor();
        _committing = false;
        _callbacks.SaveSlot(_row.DateKey, slot, text);
        return true;
    }

    private void Build()
    {
        _grid.Children.Clear();
        SheetLayout.AddColumns(_grid, _punchCount);
        _grid.Padding = Look.Thickness("ListCellPadding", new Thickness(8, 6, 8, 6));
        _grid.Background = Theme.Brush(this, Tokens.Surface);
        _frame.BorderBrush = Theme.Brush(this, Tokens.Line);
        _frame.BorderThickness = new Thickness(0, 0, 0, 1);

        var holiday = new CheckBox
        {
            IsChecked = _row.Holiday,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 0,
        };
        Look.Apply(holiday, "AppCheckBox");
        holiday.Click += (_, _) => _callbacks.ToggleHoliday(_row.DateKey);
        Place(holiday, 0);

        Place(Cell(_row.Weekday), 1);
        Place(Cell(_row.Date), 2);
        for (int slot = 0; slot < _punchCount; slot++)
        {
            Place(PunchCell(slot), SheetLayout.PunchColumn(slot));
        }

        Place(Cell(_row.Balance), SheetLayout.BalanceColumn(_punchCount));
        Place(Cell(_row.LeaveText), SheetLayout.LeaveColumn(_punchCount));
        Place(Actions(), SheetLayout.ActionsColumn(_punchCount));
        if (_row.OnLeave || _row.Incomplete)
        {
            var mark = new Border
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Theme.Brush(this, _row.OnLeave ? Tokens.Success : Tokens.Danger),
            };
            Grid.SetColumnSpan(mark, _grid.ColumnDefinitions.Count);
            _grid.Children.Add(mark);
        }
    }

    private Border PunchCell(int slot)
    {
        var label = Cell(_row.Slots[slot]);
        var hit = new Border
        {
            Background = Theme.Brush(this, Tokens.Surface),
            Child = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        int column = SheetLayout.PunchColumn(slot);
        hit.Tapped += (_, _) =>
        {
            if (_callbacks.CommitOthers(this))
            {
                return;
            }

            BeginEdit(slot, column, _row.Slots[slot]);
        };
        return hit;
    }

    private void BeginEdit(int slot, int column, string current)
    {
        if (_editor is not null)
        {
            CommitEdit();
            return;
        }

        var box = new AppTextBox
        {
            Text = current,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        box.KeyDown += OnEditorKey;
        box.LostFocus += (_, _) => CommitEdit();
        Grid.SetColumn(box, column);
        _grid.Children.Add(box);
        _editor = box;
        _editSlot = slot;
        box.Loaded += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
    }

    private void OnEditorKey(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            CommitEdit();
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Escape)
        {
            CloseEditor();
            args.Handled = true;
        }
    }

    private void CloseEditor()
    {
        if (_editor is not null)
        {
            _grid.Children.Remove(_editor);
        }

        _editor = null;
        _editSlot = -1;
    }

    private StackPanel Actions()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Look.Number("FieldGap", 8),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var reset = new AppButton
        {
            Content = AppMessages.CalculationReset,
            Kind = AppButtonKind.CompactSecondary,
        };
        reset.Click += (_, _) => _callbacks.ResetDay(_row.DateKey);
        row.Children.Add(reset);
        if (_row.CanAddLeave)
        {
            var leave = new AppButton
            {
                Content = AppMessages.CalculationAddLeave,
                Kind = AppButtonKind.CompactPrimary,
            };
            leave.Click += (_, _) => _callbacks.AddLeave(_row.DateKey);
            row.Children.Add(leave);
        }

        return row;
    }

    private static TextBlock Cell(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Look.Apply(block, "AppListCellText");
        return block;
    }

    private void Place(FrameworkElement child, int column)
    {
        Grid.SetColumn(child, column);
        _grid.Children.Add(child);
    }
}

internal sealed class SheetDayCallbacks
{
    public required Action<string> ToggleHoliday { get; init; }

    public required Action<string, int, string> SaveSlot { get; init; }

    public required Action<string> ResetDay { get; init; }

    public required Action<string> AddLeave { get; init; }

    public required Func<SheetDayRowView, bool> CommitOthers { get; init; }
}

internal static class Look
{
    public static void Apply(FrameworkElement target, string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out object? value) && value is Style style)
        {
            target.Style = style;
        }
    }

    public static Thickness Thickness(string key, Thickness fallback)
    {
        return Application.Current.Resources.TryGetValue(key, out object? value) && value is Thickness thickness
            ? thickness
            : fallback;
    }

    public static double Number(string key, double fallback)
    {
        return Application.Current.Resources.TryGetValue(key, out object? value) && value is double number
            ? number
            : fallback;
    }
}
