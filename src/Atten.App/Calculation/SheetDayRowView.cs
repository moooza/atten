using Atten.Core;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Atten.App.Calculation;

internal sealed class SheetDayRowView : UserControl
{
    private SheetDayRow _row;
    private readonly int _punchCount;
    private readonly SheetDayCallbacks _callbacks;
    private readonly Border _frame = new();
    private readonly Grid _grid = new();
    private ClockTimeField? _editor;
    private AppPopup? _popup;
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
        Build();
        ActualThemeChanged += (_, _) =>
        {
            CloseEditor();
            _grid.Children.Clear();
            Build();
        };
    }

    public string DateKey => _row.DateKey;

    public void Bind(SheetDayRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        CloseEditor();
        _row = row;
        Build();
    }

    public bool CommitEdit()
    {
        if (_editor is null || _editSlot < 0 || _committing)
        {
            return false;
        }

        _committing = true;
        string text = _editor.CommitText;
        int slot = _editSlot;
        CloseEditor();
        _committing = false;
        _callbacks.SaveSlot(_row.DateKey, slot, text);
        return true;
    }

    public void CancelEdit()
    {
        CloseEditor();
    }

    private void ShiftSlots(bool forward)
    {
        if (_editor is null || _editSlot < 0 || _committing)
        {
            return;
        }

        try
        {
            var working = _row.Record.Slots.ToList();
            while (working.Count <= _editSlot)
            {
                working.Add(null);
            }

            working[_editSlot] = Sheet.ParseClock(_editor.CommitText);
            IReadOnlyList<string?> shifted = forward
                ? Sheet.ShiftSlotsForward(working, _editSlot)
                : Sheet.ShiftSlotsBackward(working, _editSlot);
            CloseEditor();
            _callbacks.SaveSlots(_row.DateKey, shifted);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            _callbacks.ShowNotice(ex.Message);
        }
    }

    private void Build()
    {
        _grid.Children.Clear();
        SheetLayout.AddColumns(_grid, _punchCount);
        _grid.Padding = Look.Thickness("ListCellPadding", new Thickness(8, 6, 8, 6));
        Microsoft.UI.Xaml.Media.Brush fill = Theme.Brush(this, RowToken());
        _grid.Background = fill;
        _frame.Background = fill;
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
    }

    private Border PunchCell(int slot)
    {
        var label = Cell(_row.Slots[slot]);
        var hit = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        hit.Tapped += (_, args) =>
        {
            args.Handled = true;
            _callbacks.DismissOthers(this);
            DispatcherQueue.TryEnqueue(() => BeginEdit(slot, hit, _row.Slots[slot]));
        };
        return hit;
    }

    private void BeginEdit(int slot, FrameworkElement anchor, string current)
    {
        if (_editor is not null && _editSlot == slot)
        {
            return;
        }

        CloseEditor();
        var field = new ClockTimeField
        {
            Text = current,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        field.Confirmed += (_, _) => CommitEdit();
        field.ShiftedLeft += (_, _) => ShiftSlots(forward: true);
        field.ShiftedRight += (_, _) => ShiftSlots(forward: false);
        field.Cancelled += (_, _) => CloseEditor();
        var popup = new AppPopup
        {
            Content = field,
            Placement = FlyoutPlacementMode.Bottom,
            MinPresenterWidth = field.MinWidth,
        };
        popup.Closed += OnPopupClosed;
        _editor = field;
        _popup = popup;
        _editSlot = slot;
        popup.ShowAt(anchor);
        field.Loaded += (_, _) => field.FocusInput();
    }

    private void OnPopupClosed(object? sender, object args)
    {
        CloseEditor();
    }

    private void CloseEditor()
    {
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.Hide();
            _popup = null;
        }

        _editor = null;
        _editSlot = -1;
    }

    private string RowToken()
    {
        if (_row.OnLeave && _row.Record.BalanceMinutes >= 0)
        {
            return Tokens.SuccessSoft;
        }

        if (_row.Holiday)
        {
            return Tokens.DangerSoft;
        }

        if (_row.Record.BalanceMinutes < 0 || _row.Incomplete)
        {
            return Tokens.WarningSoft;
        }

        return Tokens.Surface;
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

    public required Action<string, IReadOnlyList<string?>> SaveSlots { get; init; }

    public required Action<string> ShowNotice { get; init; }

    public required Action<string> ResetDay { get; init; }

    public required Action<string> AddLeave { get; init; }

    public required Action<SheetDayRowView> DismissOthers { get; init; }
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
