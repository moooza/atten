using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Calculation;

public sealed partial class CalculationPage : Page
{
    private readonly IPersonnelRepository? _people;
    private readonly IClockEventRepository? _clocks;
    private readonly ICalculationRepository? _store;
    private readonly ILeaveRepository? _leaves;
    private readonly Dictionary<string, SheetRow> _rows = new(StringComparer.Ordinal);
    private string? _remoteId;
    private long? _personnelId;
    private double? _dailyHours;
    private DateOnly? _start;
    private DateOnly? _end;
    private readonly List<SheetDayRowView> _views = [];

    public CalculationPage()
    {
        InitializeComponent();
        IAttenRepository? appStore = App.Current.Store;
        _people = appStore?.Personnel;
        _clocks = appStore?.ClockEvents;
        _store = appStore?.Calculation;
        _leaves = appStore?.Leaves;
        PersonPicker.Source = _people;
        PersonPicker.SelectionChanged += (_, _) => ShowIfReady();
        PersonPicker.LoadFailed += (_, _) =>
        {
            Notice.Text = PersonPicker.Error ?? AppMessages.PersonnelLoadFailed;
            ShowEmpty(PersonPicker.Error ?? AppMessages.PersonnelLoadFailed);
        };
        StartField.DateChanged += (_, _) => ShowIfReady();
        EndField.DateChanged += (_, _) => ShowIfReady();
        Editor.Saved += (_, _) =>
        {
            HideForm();
            ShowResult();
        };
        Editor.Cancelled += (_, _) => HideForm();
        Loaded += (_, _) =>
        {
            PersonPicker.Reload();
            if (!string.IsNullOrEmpty(PersonPicker.Error))
            {
                Notice.Text = PersonPicker.Error;
            }
        };
    }

    private void OnShow(object sender, RoutedEventArgs args)
    {
        ShowResult();
    }

    private void OnEnterAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FormHost.Visibility == Visibility.Visible)
        {
            return;
        }

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
        if (CommitPending())
        {
            return;
        }

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
        CommitPending();
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
        if (CommitPending())
        {
            return;
        }

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
        if (CommitPending())
        {
            return;
        }

        if (FormHost.Visibility == Visibility.Visible)
        {
            return;
        }

        if (!TryContext(storedDate, out SheetRow row) || row.BalanceMinutes >= 0 || _personnelId is null)
        {
            return;
        }

        if (_leaves is null || _people is null)
        {
            Notice.Text = AppMessages.LeaveListFailed;
            return;
        }

        Editor.Bind(
            _leaves,
            _people,
            _personnelId,
            leave: null,
            startDate: Day(storedDate),
            endDate: Day(storedDate),
            minutes: Math.Abs(row.BalanceMinutes));
        SheetRoot.Visibility = Visibility.Collapsed;
        FormHost.Visibility = Visibility.Visible;
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
        Dictionary<string, bool> holidays = _store.ListBalances(remoteId, start, end)
            .ToDictionary(row => row.Date, row => row.Holiday, StringComparer.Ordinal);
        IReadOnlyList<LeaveSpan> leaves = _leaves.List(personnelId, start, end)
            .Select(row => new LeaveSpan(Day(row.StartDate), Day(row.EndDate), row.Minutes))
            .ToList();
        IReadOnlyList<SheetRow> rows = Sheet.BuildSheet(
            start,
            end,
            device,
            overrides,
            dailyHours,
            holidays,
            leaves);
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
        _views.Clear();
        SheetHost.Children.Clear();
        _rows.Clear();
        foreach (SheetRow row in rows)
        {
            _rows[row.Date] = row;
        }

        if (rows.Count == 0)
        {
            ShowEmpty(AppMessages.CalculationChooseRange);
            return;
        }

        int punchCount = SheetLayout.PunchCount(rows);
        SheetHost.Children.Add(BuildHeader(punchCount));
        var callbacks = new SheetDayCallbacks
        {
            ToggleHoliday = ToggleHoliday,
            SaveSlot = SaveSlot,
            ResetDay = ResetDay,
            AddLeave = AddLeave,
            CommitOthers = CommitOthers,
        };
        foreach (SheetRow row in rows)
        {
            var view = new SheetDayRowView(new SheetDayRow(row, punchCount), punchCount, callbacks);
            _views.Add(view);
            SheetHost.Children.Add(view);
        }

        EmptyText.Visibility = Visibility.Collapsed;
        TablePanel.Visibility = Visibility.Visible;
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
        var block = new StackPanel();
        block.Children.Add(header);
        block.Children.Add(new Border
        {
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Theme.Brush(this, Tokens.Line),
        });
        return block;
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
        _views.Clear();
        SheetHost.Children.Clear();
        _rows.Clear();
        EmptyText.Text = message;
        EmptyText.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
    }

    private void HideForm()
    {
        FormHost.Visibility = Visibility.Collapsed;
        SheetRoot.Visibility = Visibility.Visible;
    }

    private bool CommitPending()
    {
        return CommitOthers(except: null);
    }

    private bool CommitOthers(SheetDayRowView? except)
    {
        foreach (SheetDayRowView view in _views)
        {
            if (!ReferenceEquals(view, except) && view.CommitEdit())
            {
                return true;
            }
        }

        return false;
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

    private async Task<bool> ConfirmReset(string shamsiDate)
    {
        var dialog = new ContentDialog
        {
            Title = AppMessages.CalculationResetTitle,
            Content = AppMessages.CalculationResetPrompt(shamsiDate),
            PrimaryButtonText = AppMessages.CalculationReset,
            CloseButtonText = "انصراف",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            FlowDirection = FlowDirection.RightToLeft,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
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
