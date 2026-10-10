using Atten.Core;
using Atten.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Leaves;

public sealed partial class LeavesPage : Page
{
    private readonly ILeaveRepository? _store;
    private readonly IPersonnelRepository? _people;
    private readonly HashSet<int> _checkedYears = [Leave.ShamsiYearOf(DateOnly.FromDateTime(DateTime.Today))];
    private long? _yearsPersonId;

    public LeavesPage()
    {
        InitializeComponent();
        IAttenRepository? appStore = App.Current.Store;
        _store = appStore?.Leaves;
        _people = appStore?.Personnel;
        PersonPicker.Source = _people;
        PersonPicker.SelectionChanged += (_, _) => Load(resetYears: true);
        PersonPicker.LoadFailed += (_, _) =>
        {
            Notice.Text = PersonPicker.Error ?? AppMessages.PersonnelLoadFailed;
            ShowEmpty(PersonPicker.Error ?? AppMessages.PersonnelLoadFailed);
        };
        YearDropdown.SelectionChanged += OnYearsChanged;
        Editor.Saved += (_, _) =>
        {
            HideForm();
            Reload(keepYears: true);
        };
        Editor.Cancelled += (_, _) => HideForm();
        Loaded += (_, _) => Reload(keepYears: false);
    }

    private void OnAdd(object sender, RoutedEventArgs args)
    {
        Notice.Text = string.Empty;
        OpenForm(PersonPicker.SelectedPersonnelId, leave: null);
    }

    private void OnEdit(object sender, RoutedEventArgs args)
    {
        OpenSelected();
    }

    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        OpenSelected();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        Notice.Text = string.Empty;
    }

    private void OnYearsChanged(object? sender, EventArgs args)
    {
        _checkedYears.Clear();
        foreach (int year in YearDropdown.SelectedYears)
        {
            _checkedYears.Add(year);
        }

        Load(resetYears: false, bindYears: false);
    }

    private void OpenSelected()
    {
        if (LeavesList.SelectedItem is not LeaveRow row)
        {
            Notice.Text = AppMessages.LeaveSelectOne;
            return;
        }

        Notice.Text = string.Empty;
        OpenForm(row.Record.PersonnelId, row.Record);
    }

    private void OpenForm(long? personnelId, LeaveRecord? leave)
    {
        if (_store is null || _people is null)
        {
            Notice.Text = AppMessages.LeaveListFailed;
            return;
        }

        Editor.Bind(_store, _people, personnelId, leave);
        ShowForm();
    }

    private void Reload(bool keepYears)
    {
        Notice.Text = string.Empty;
        PersonPicker.Reload();
        if (!string.IsNullOrEmpty(PersonPicker.Error))
        {
            Notice.Text = PersonPicker.Error;
        }

        Load(resetYears: !keepYears);
    }

    private void Load(bool resetYears, bool bindYears = true)
    {
        PersonnelRecord? person = PersonPicker.SelectedPerson;
        DateOnly? started = Leave.OptionalDate(person?.CooperationStart);
        DateOnly? ended = Leave.OptionalDate(person?.CooperationEnd);
        IReadOnlyList<int> choices = Leave.CooperationYearChoices(started, ended);
        SelectYears(person?.Id, choices, resetYears);
        if (bindYears)
        {
            YearDropdown.Bind(choices, _checkedYears);
        }

        if (person is null)
        {
            CountText.Text = string.Empty;
            YearCards.ItemsSource = null;
            ShowEmpty(AppMessages.ChoosePersonnel);
            return;
        }

        if (_store is null)
        {
            CountText.Text = string.Empty;
            YearCards.ItemsSource = null;
            ShowEmpty(AppMessages.LeaveListFailed);
            return;
        }

        try
        {
            ShowRows(person, _store.List(person.Id));
        }
        catch (Exception ex)
        {
            CountText.Text = string.Empty;
            YearCards.ItemsSource = null;
            ShowEmpty($"{AppMessages.LeaveListFailed}: {ex.Message}");
        }
    }

    private void ShowRows(PersonnelRecord person, IReadOnlyList<LeaveRecord> rows)
    {
        DateOnly? started = Leave.OptionalDate(person.CooperationStart);
        DateOnly? ended = Leave.OptionalDate(person.CooperationEnd);
        var usedByYear = new Dictionary<int, int>();
        foreach (LeaveRecord row in rows)
        {
            int year = Leave.ShamsiYearOf(row.StartDate);
            usedByYear[year] = usedByYear.GetValueOrDefault(year) + row.Minutes;
        }

        IReadOnlyDictionary<int, LeaveYearSettlement> settled = Leave.SettleCooperationYears(
            started,
            ended,
            usedByYear);
        var cards = _checkedYears
            .OrderByDescending(year => year)
            .Select(year => new LeaveYearCard(Leave.YearBalance(year, started, settled, usedByYear)))
            .ToList();
        YearCards.ItemsSource = cards;

        IReadOnlyList<LeaveRow> visible = rows
            .Where(row => _checkedYears.Contains(Leave.ShamsiYearOf(row.StartDate)))
            .Select(row => new LeaveRow(row))
            .ToList();
        LeavesList.ItemsSource = visible;
        if (visible.Count > 0)
        {
            CountText.Text = AppMessages.LeaveCount(visible.Count);
            EmptyText.Visibility = Visibility.Collapsed;
            TablePanel.Visibility = Visibility.Visible;
            return;
        }

        CountText.Text = string.Empty;
        string empty = !_checkedYears.Any()
            ? AppMessages.LeaveChooseYear
            : rows.Count > 0
                ? AppMessages.LeaveEmptyYears
                : AppMessages.LeaveEmpty;
        ShowEmpty(empty);
    }

    private void SelectYears(long? personId, IReadOnlyList<int> choices, bool reset)
    {
        int current = Leave.ShamsiYearOf(DateOnly.FromDateTime(DateTime.Today));
        if (reset || _yearsPersonId != personId)
        {
            _checkedYears.Clear();
            _checkedYears.Add(current);
        }
        else
        {
            _checkedYears.IntersectWith(choices);
        }

        _yearsPersonId = personId;
    }

    private void ShowEmpty(string message)
    {
        EmptyText.Text = message;
        EmptyText.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
        LeavesList.ItemsSource = null;
    }

    private void ShowForm()
    {
        ListRoot.Visibility = Visibility.Collapsed;
        FormHost.Visibility = Visibility.Visible;
    }

    private void HideForm()
    {
        FormHost.Visibility = Visibility.Collapsed;
        ListRoot.Visibility = Visibility.Visible;
    }
}
