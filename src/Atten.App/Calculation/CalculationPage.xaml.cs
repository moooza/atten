using Atten.App.Leaves;
using Atten.App.Shared;
using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace Atten.App.Calculation;

public sealed partial class CalculationPage : Page
{
    private readonly IPersonnelRepository? _people;
    private readonly IClockEventRepository? _clocks;
    private readonly ICalculationRepository? _store;
    private readonly ILeaveRepository? _leaves;
    private readonly IOfficialHolidayRepository? _official;
    private readonly Dictionary<string, SheetRow> _rows = new(StringComparer.Ordinal);
    private string? _remoteId;
    private long? _personnelId;
    private double? _dailyHours;
    private DateOnly? _start;
    private DateOnly? _end;
    private readonly List<SheetDayRowView> _views = [];
    private FormWindow? _editorWindow;
    private LeaveForm? _editor;
    private EventHandler<object>? _restoreScroll;
    private int _punchCount;
    private int _scrollTries;
    private double _restoreHorizontal;
    private double _restoreVertical;
    private double _restoreExtentHeight;

    public CalculationPage()
    {
        InitializeComponent();
        ApplyDefaultDates();
        IAttenRepository? appStore = App.Current.Store;
        _people = appStore?.Personnel;
        _clocks = appStore?.ClockEvents;
        _store = appStore?.Calculation;
        _leaves = appStore?.Leaves;
        _official = appStore?.OfficialHolidays;
        PersonPicker.Source = _people;
        PersonPicker.SelectionChanged += (_, _) => ShowIfReady();
        PersonPicker.LoadFailed += (_, _) =>
        {
            Notice.Text = PersonPicker.Error ?? AppMessages.PersonnelLoadFailed;
            ShowEmpty(PersonPicker.Error ?? AppMessages.PersonnelLoadFailed);
        };
        StartField.DateChanged += (_, _) => ShowIfReady();
        EndField.DateChanged += (_, _) => ShowIfReady();
        Loaded += (_, _) =>
        {
            PersonPicker.Reload();
            if (!string.IsNullOrEmpty(PersonPicker.Error))
            {
                Notice.Text = PersonPicker.Error;
            }
        };
        Unloaded += (_, _) =>
        {
            CancelScrollRestore();
            _editorWindow?.Close();
        };
    }

    private void ApplyDefaultDates()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        var (year, month, _) = Dates.ShamsiYmd(today);
        StartField.GregorianDate = Dates.ShamsiMonthSpan(year, month).Start;
        EndField.GregorianDate = today;
    }

    private void OnShow(object sender, RoutedEventArgs args)
    {
        ShowResult();
    }

    private void OnEnterAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ShowResult();
        args.Handled = true;
    }

    private void ShowIfReady()
    {
        if (PersonPicker.SelectedPerson is null)
        {
            return;
        }

        if (StartField.GregorianDate is null || EndField.GregorianDate is null)
        {
            return;
        }

        ShowResult();
    }

    private void ShowResult()
    {
        DismissEdits();
        Notice.Text = string.Empty;
        try
        {
            PersonnelRecord person = PersonPicker.SelectedPerson
                ?? throw new ArgumentException(AppMessages.ChoosePersonnel);
            DateOnly start = RequiredDate(StartField, AppMessages.CalculationNeedDates);
            DateOnly end = RequiredDate(EndField, AppMessages.CalculationNeedDates);
            if (string.IsNullOrWhiteSpace(person.RemoteId))
            {
                throw new ArgumentException(AppMessages.CalculationMissingRemoteId);
            }

            IReadOnlyList<SheetRow> rows = LoadRows(person.RemoteId, person.DailyHours, start, end, person.Id);
            _remoteId = person.RemoteId;
            _personnelId = person.Id;
            _dailyHours = person.DailyHours;
            _start = start;
            _end = end;
            StartField.GregorianDate = start;
            EndField.GregorianDate = end;
            Notice.Text = string.Empty;
            Render(rows);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            ShowEmpty(ex.Message);
        }
        catch (Exception ex)
        {
            ShowEmpty($"{AppMessages.ClockListFailed}: {ex.Message}");
        }
    }

    private void ToggleHoliday(string storedDate)
    {
        DismissEdits();
        if (!TryContext(storedDate, out SheetRow row) || _remoteId is null)
        {
            return;
        }

        try
        {
            _store!.SaveHoliday(_remoteId, Day(storedDate), !row.Holiday);
            Render(ReloadCurrent());
            Notice.Text = string.Empty;
        }
        catch (Exception ex)
        {
            Notice.Text = $"{AppMessages.CalculationHolidayFailed}: {ex.Message}";
        }
    }

    private async void ResetDay(string storedDate)
    {
        DismissEdits();
        if (!TryContext(storedDate, out _) || _remoteId is null)
        {
            return;
        }

        if (!await ConfirmReset(Dates.FormatShamsiDate(storedDate)))
        {
            return;
        }

        try
        {
            _store!.ClearPunches(_remoteId, Day(storedDate));
            Render(ReloadCurrent());
            Notice.Text = string.Empty;
        }
        catch (Exception ex)
        {
            Notice.Text = $"{AppMessages.CalculationResetFailed}: {ex.Message}";
        }
    }

    private void AddLeave(string storedDate)
    {
        DismissEdits();
        if (!TryContext(storedDate, out SheetRow row) || row.BalanceMinutes >= 0 || _personnelId is null)
        {
            return;
        }

        if (_leaves is null || _people is null)
        {
            Notice.Text = AppMessages.LeaveListFailed;
            return;
        }

        if (_editorWindow is null || _editor is null)
        {
            _editor = new LeaveForm();
            _editor.Saved += OnEditorSaved;
            _editor.Cancelled += OnEditorCancelled;
            _editorWindow = new FormWindow(AppMessages.LeaveAdd, _editor, new SizeInt32(580, 700), fitContent: true);
            _editorWindow.Closed += OnEditorClosed;
        }
        else
        {
            _editorWindow.Title = AppMessages.LeaveAdd;
        }

        _editor.Bind(
            _leaves,
            _people,
            _personnelId,
            leave: null,
            startDate: Day(storedDate),
            endDate: Day(storedDate),
            minutes: Math.Abs(row.BalanceMinutes));
        _editorWindow.BringForward();
    }

    private void OnEditorSaved(object? sender, EventArgs args)
    {
        _editorWindow?.Close();
        ShowResult();
    }

    private void OnEditorCancelled(object? sender, EventArgs args)
    {
        _editorWindow?.Close();
    }

    private void OnEditorClosed(object sender, WindowEventArgs args)
    {
        if (_editor is not null)
        {
            _editor.Saved -= OnEditorSaved;
            _editor.Cancelled -= OnEditorCancelled;
        }

        _editorWindow = null;
        _editor = null;
    }

    private void SaveSlot(string storedDate, int slot, string text)
    {
        if (_remoteId is null || _personnelId is null || _dailyHours is null || _start is null || _end is null)
        {
            Notice.Text = AppMessages.ChoosePersonnel;
            return;
        }

        try
        {
            _store!.SavePunch(_remoteId, Day(storedDate), slot, Sheet.ParseClock(text));
            Render(ReloadCurrent());
            Notice.Text = string.Empty;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Notice.Text = ex.Message;
        }
        catch (Exception ex)
        {
            Notice.Text = $"{AppMessages.CalculationTimeFailed}: {ex.Message}";
        }
    }

    private void SaveSlots(string storedDate, IReadOnlyList<string?> slots)
    {
        if (_remoteId is null || _personnelId is null || _dailyHours is null || _start is null || _end is null)
        {
            Notice.Text = AppMessages.ChoosePersonnel;
            return;
        }

        try
        {
            DateOnly day = Day(storedDate);
            int previous = _rows.TryGetValue(storedDate, out SheetRow? row) ? row.Slots.Count : 0;
            int count = Math.Max(slots.Count, previous);
            for (int slot = 0; slot < count; slot++)
            {
                _store!.SavePunch(_remoteId, day, slot, slot < slots.Count ? slots[slot] : null);
            }

            Render(ReloadCurrent());
            Notice.Text = string.Empty;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Notice.Text = ex.Message;
        }
        catch (Exception ex)
        {
            Notice.Text = $"{AppMessages.CalculationTimeFailed}: {ex.Message}";
        }
    }

    private IReadOnlyList<SheetRow> LoadRows(
        string remoteId,
        double dailyHours,
        DateOnly start,
        DateOnly end,
        long personnelId)
    {
        if (_clocks is null || _store is null || _leaves is null)
        {
            throw new InvalidOperationException(AppMessages.ClockListFailed);
        }

        IReadOnlyList<DeviceDay> device = _clocks.ListDaily(remoteId, start, end);
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, string?>> overrides =
            _store.ListPunches(remoteId, start, end);
        IReadOnlySet<string> official = OfficialHoliday.Catalog(
            (_official?.List(start, end) ?? []).Select(row => row.Date));
        Dictionary<string, bool> holidays = _store.ListBalances(remoteId, start, end)
            .Where(row => row.HolidayOverride is not null)
            .ToDictionary(row => row.Date, row => row.HolidayOverride!.Value, StringComparer.Ordinal);
        IReadOnlyList<LeaveSpan> leaves = _leaves.List(personnelId, start, end)
            .Where(row => row.Kind == Leave.KindLeave)
            .Select(row => new LeaveSpan(Day(row.StartDate), Day(row.EndDate), row.Minutes))
            .ToList();
        IReadOnlyList<SheetRow> rows = Sheet.BuildSheet(
            start,
            end,
            device,
            overrides,
            dailyHours,
            holidays,
            leaves,
            official);
        _store.SaveBalances(
            remoteId,
            rows.Select(row => (Day(row.Date), row.BalanceMinutes, row.Holiday)));
        return rows;
    }

    private IReadOnlyList<SheetRow> ReloadCurrent()
    {
        return LoadRows(_remoteId!, _dailyHours!.Value, _start!.Value, _end!.Value, _personnelId!.Value);
    }

    private void Render(IReadOnlyList<SheetRow> rows)
    {
        if (rows.Count == 0)
        {
            ShowEmpty(AppMessages.CalculationChooseRange);
            return;
        }

        int punchCount = SheetLayout.PunchCount(rows);
        if (TryBindRows(rows, punchCount))
        {
            BindRangeTotals(rows);
            return;
        }

        double horizontal = BodyScroll.HorizontalOffset;
        double vertical = BodyScroll.VerticalOffset;
        double extentHeight = BodyScroll.ExtentHeight;
        bool keepScroll = TablePanel.Visibility == Visibility.Visible;
        CancelScrollRestore();
        _views.Clear();
        HeaderHost.Child = null;
        SheetHost.Children.Clear();
        RememberRows(rows);

        HeaderHost.Child = BuildHeader(punchCount);
        SheetDayCallbacks callbacks = DayCallbacks();
        foreach (SheetRow row in rows)
        {
            var view = new SheetDayRowView(new SheetDayRow(row, punchCount), punchCount, callbacks);
            _views.Add(view);
            SheetHost.Children.Add(view);
        }

        _punchCount = punchCount;
        EmptyText.Visibility = Visibility.Collapsed;
        TablePanel.Visibility = Visibility.Visible;
        BindRangeTotals(rows);
        if (keepScroll)
        {
            RestoreSheetScroll(horizontal, vertical, extentHeight);
        }
    }

    private bool TryBindRows(IReadOnlyList<SheetRow> rows, int punchCount)
    {
        if (TablePanel.Visibility != Visibility.Visible
            || punchCount != _punchCount
            || rows.Count != _views.Count)
        {
            return false;
        }

        for (int index = 0; index < rows.Count; index++)
        {
            if (!string.Equals(rows[index].Date, _views[index].DateKey, StringComparison.Ordinal))
            {
                return false;
            }
        }

        RememberRows(rows);
        for (int index = 0; index < rows.Count; index++)
        {
            _views[index].Bind(new SheetDayRow(rows[index], punchCount));
        }

        return true;
    }

    private void RememberRows(IReadOnlyList<SheetRow> rows)
    {
        _rows.Clear();
        foreach (SheetRow row in rows)
        {
            _rows[row.Date] = row;
        }
    }

    private SheetDayCallbacks DayCallbacks()
    {
        return new SheetDayCallbacks
        {
            ToggleHoliday = ToggleHoliday,
            SaveSlot = SaveSlot,
            SaveSlots = SaveSlots,
            ShowNotice = message => Notice.Text = message,
            ResetDay = ResetDay,
            AddLeave = AddLeave,
            DismissOthers = DismissOthers,
        };
    }

    private void RestoreSheetScroll(double horizontal, double vertical, double previousHeight)
    {
        _restoreHorizontal = horizontal;
        _restoreVertical = vertical;
        _restoreExtentHeight = previousHeight;
        _scrollTries = 0;
        _restoreScroll = OnRestoreSheetLayout;
        BodyScroll.LayoutUpdated += _restoreScroll;
        DispatcherQueue.TryEnqueue(() => OnRestoreSheetLayout(null, EventArgs.Empty));
    }

    private void OnRestoreSheetLayout(object? sender, object args)
    {
        _scrollTries += 1;
        bool tallEnough = BodyScroll.ExtentHeight >= _restoreVertical
            && (BodyScroll.ExtentHeight >= _restoreExtentHeight * 0.85 || _scrollTries >= 8);
        if (!tallEnough && _scrollTries < 24)
        {
            return;
        }

        BodyScroll.ChangeView(_restoreHorizontal, _restoreVertical, zoomFactor: null, disableAnimation: true);
        HeaderScroll.ChangeView(_restoreHorizontal, 0, zoomFactor: null, disableAnimation: true);
        bool placed = Math.Abs(BodyScroll.VerticalOffset - _restoreVertical) < 2
            && Math.Abs(BodyScroll.HorizontalOffset - _restoreHorizontal) < 2;
        if (placed || _scrollTries >= 24)
        {
            CancelScrollRestore();
        }
    }

    private void CancelScrollRestore()
    {
        if (_restoreScroll is null)
        {
            return;
        }

        BodyScroll.LayoutUpdated -= _restoreScroll;
        _restoreScroll = null;
        _scrollTries = 0;
    }

    private FrameworkElement BuildHeader(int punchCount)
    {
        var header = new Grid { Padding = Look.Thickness("ListCellPadding", new Thickness(8, 6, 8, 6)) };
        SheetLayout.AddColumns(header, punchCount);
        PlaceHeader(header, "روز تعطیل", 0);
        PlaceHeader(header, "روز", 1);
        PlaceHeader(header, "تاریخ", 2);
        for (int slot = 0; slot < punchCount; slot++)
        {
            PlaceHeader(header, SheetLayout.PunchTitle(slot), SheetLayout.PunchColumn(slot));
        }

        PlaceHeader(header, "کسری / اضافه", SheetLayout.BalanceColumn(punchCount));
        PlaceHeader(header, "مرخصی", SheetLayout.LeaveColumn(punchCount));
        PlaceHeader(header, "عملیات", SheetLayout.ActionsColumn(punchCount));
        return header;
    }

    private void OnSheetScroll(object sender, ScrollViewerViewChangedEventArgs args)
    {
        HeaderScroll.ChangeView(BodyScroll.HorizontalOffset, 0, null, disableAnimation: true);
    }

    private static void PlaceHeader(Grid header, string title, int column)
    {
        var block = new TextBlock { Text = title };
        Look.Apply(block, "AppListHeaderText");
        Grid.SetColumn(block, column);
        header.Children.Add(block);
    }

    private void ShowEmpty(string message)
    {
        CancelScrollRestore();
        _views.Clear();
        _punchCount = 0;
        HeaderHost.Child = null;
        SheetHost.Children.Clear();
        _rows.Clear();
        EmptyText.Text = message;
        EmptyText.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
        RangeTotals.Text = string.Empty;
        RangeTotals.Visibility = Visibility.Collapsed;
    }

    private void BindRangeTotals(IReadOnlyList<SheetRow> rows)
    {
        int leaveMinutes = 0;
        foreach (SheetRow row in rows)
        {
            if (row.LeaveMinutes > 0)
            {
                leaveMinutes += row.LeaveMinutes;
            }
        }

        PayrollTotals totals = Payroll.Summarize(rows.Select(row => row.BalanceMinutes), leaveMinutes, 0);
        string text = Payroll.FormatRangeTotals(totals);
        RangeTotals.Text = text;
        RangeTotals.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DismissEdits()
    {
        DismissOthers(except: null);
    }

    private void DismissOthers(SheetDayRowView? except)
    {
        foreach (SheetDayRowView view in _views)
        {
            if (!ReferenceEquals(view, except))
            {
                view.CancelEdit();
            }
        }
    }

    private bool TryContext(string storedDate, out SheetRow row)
    {
        row = null!;
        return _remoteId is not null
            && _personnelId is not null
            && _dailyHours is not null
            && _start is not null
            && _end is not null
            && _store is not null
            && _rows.TryGetValue(storedDate, out row!);
    }

    private Task<bool> ConfirmReset(string shamsiDate)
    {
        string person = PersonPicker.SelectedPerson is PersonnelRecord chosen
            ? PersonnelPicker.FormatLabel(chosen)
            : AppMessages.ChoosePersonnel;
        return AppConfirmDialog.ShowAsync(
            XamlRoot,
            ActualTheme,
            new AppConfirmRequest
            {
                Title = AppMessages.CalculationResetTitle,
                Ask = AppMessages.CalculationResetAsk,
                Lines =
                [
                    new AppConfirmLine("کارمند: ", person),
                    new AppConfirmLine("تاریخ: ", shamsiDate),
                ],
                ConfirmText = AppMessages.CalculationReset,
            });
    }

    private static DateOnly RequiredDate(ShamsiDateField field, string empty)
    {
        if (string.IsNullOrWhiteSpace(field.Text) && field.GregorianDate is null)
        {
            throw new ArgumentException(empty);
        }

        return field.GregorianDate ?? Dates.ParseShamsiDate(field.Text);
    }

    private static DateOnly Day(string stored)
    {
        return Leave.OptionalDate(stored) ?? throw new FormatException("تاریخ معتبر نیست.");
    }
}
