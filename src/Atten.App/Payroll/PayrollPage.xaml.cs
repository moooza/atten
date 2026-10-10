using Atten.Core;
using Atten.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CorePayroll = Atten.Core.Payroll;

namespace Atten.App.Payroll;

public sealed partial class PayrollPage : Page
{
    private readonly IPayrollRepository? _payroll;
    private readonly IPersonnelRepository? _people;
    private readonly IUserRepository? _users;
    private bool _bindingPeriod;

    public PayrollPage()
    {
        InitializeComponent();
        IAttenRepository? store = App.Current.Store;
        _payroll = store?.Payroll;
        _people = store?.Personnel;
        _users = store?.Users;
        PersonPicker.Source = _people;
        PersonPicker.SelectionChanged += (_, _) =>
        {
            BindPeriod(PersonPicker.SelectedPerson);
            Load();
        };
        PersonPicker.LoadFailed += (_, _) =>
        {
            Notice.Text = PersonPicker.Error ?? AppMessages.PersonnelLoadFailed;
            ShowEmpty(PersonPicker.Error ?? AppMessages.PersonnelLoadFailed);
        };
        YearCombo.SelectionChanged += (_, _) =>
        {
            if (!_bindingPeriod)
            {
                Load();
            }
        };
        MonthCombo.SelectionChanged += (_, _) =>
        {
            if (!_bindingPeriod)
            {
                Load();
            }
        };
        Loaded += (_, _) => Reload();
    }

    private void OnDraft(object sender, RoutedEventArgs args)
    {
        Run(store =>
        {
            var (start, end) = SelectedPeriod();
            long personId = SelectedPersonId();
            store.Draft(personId, start, end);
            Notice.Text = string.Empty;
        });
    }

    private void OnConfirm(object sender, RoutedEventArgs args)
    {
        Run(store =>
        {
            store.Confirm(SelectedRunId(store));
            Notice.Text = AppMessages.PayrollConfirmed;
        });
    }

    private void OnReopen(object sender, RoutedEventArgs args)
    {
        Run(store =>
        {
            store.Reopen(SelectedRunId(store));
            Notice.Text = AppMessages.PayrollReopened;
        });
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (Notice.Text == AppMessages.PayrollSelectOne)
        {
            Notice.Text = string.Empty;
        }
    }

    private void Run(Action<IPayrollRepository> action)
    {
        if (_payroll is null)
        {
            Notice.Text = AppMessages.PayrollListFailed;
            return;
        }

        try
        {
            action(_payroll);
            Load();
        }
        catch (Exception ex)
        {
            Notice.Text = ex.Message;
        }
    }

    private void Reload()
    {
        Notice.Text = string.Empty;
        BindMonths();
        PersonPicker.Reload();
        if (!string.IsNullOrEmpty(PersonPicker.Error))
        {
            Notice.Text = PersonPicker.Error;
        }

        BindPeriod(PersonPicker.SelectedPerson);
        Load();
    }

    private void Load()
    {
        PersonnelRecord? person = PersonPicker.SelectedPerson;
        if (person is null)
        {
            CountText.Text = string.Empty;
            ShowEmpty(AppMessages.ChoosePersonnel);
            return;
        }

        if (_payroll is null)
        {
            CountText.Text = string.Empty;
            ShowEmpty(AppMessages.PayrollListFailed);
            return;
        }

        try
        {
            IReadOnlyDictionary<long, string> names = ActorNames();
            IReadOnlyList<PayrollRow> rows = _payroll
                .List(person.Id)
                .Select(row => new PayrollRow(row, ActorName(names, row.CreatedBy)))
                .ToList();
            PayrollList.ItemsSource = rows;
            if (rows.Count == 0)
            {
                CountText.Text = string.Empty;
                ShowEmpty(AppMessages.PayrollEmpty);
                return;
            }

            CountText.Text = AppMessages.PayrollCount(rows.Count);
            EmptyText.Visibility = Visibility.Collapsed;
            TablePanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            CountText.Text = string.Empty;
            ShowEmpty($"{AppMessages.PayrollListFailed}: {ex.Message}");
        }
    }

    private void BindPeriod(PersonnelRecord? person)
    {
        _bindingPeriod = true;
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        var (year, month, _) = Dates.ShamsiYmd(today);
        IReadOnlyList<int> years = Leave.CooperationYearChoices(
            Leave.OptionalDate(person?.CooperationStart),
            Leave.OptionalDate(person?.CooperationEnd));
        int selected = YearCombo.SelectedItem is int current && years.Contains(current) ? current : year;
        YearCombo.ItemsSource = years;
        YearCombo.SelectedItem = years.Contains(selected) ? selected : years.FirstOrDefault();
        MonthCombo.SelectedValue = month;
        _bindingPeriod = false;
    }

    private void BindMonths()
    {
        if (MonthCombo.Items.Count > 0)
        {
            return;
        }

        var months = Dates.ShamsiMonthNames
            .Select((name, index) => new MonthChoice(index + 1, name))
            .ToList();
        MonthCombo.ItemsSource = months;
    }

    private long SelectedPersonId()
    {
        return PersonPicker.SelectedPersonnelId
            ?? throw new ArgumentException(AppMessages.ChoosePersonnel);
    }

    private (DateOnly Start, DateOnly End) SelectedPeriod()
    {
        if (YearCombo.SelectedItem is not int year || MonthCombo.SelectedValue is not int month)
        {
            throw new ArgumentException(AppMessages.PayrollNeedPeriod);
        }

        return CorePayroll.MonthSpan(year, month);
    }

    private long SelectedRunId(IPayrollRepository store)
    {
        if (PayrollList.SelectedItem is PayrollRow row)
        {
            return row.Record.Id;
        }

        var (start, end) = SelectedPeriod();
        PayrollRunRecord? match = store
            .List(SelectedPersonId())
            .FirstOrDefault(item => item.StartDate == Dates.StorageDate(start) && item.EndDate == Dates.StorageDate(end));
        return match?.Id ?? throw new ArgumentException(AppMessages.PayrollSelectOne);
    }

    private IReadOnlyDictionary<long, string> ActorNames()
    {
        if (_users is null)
        {
            return new Dictionary<long, string> { [DefaultUser.Id] = DefaultUser.DisplayName };
        }

        return _users.List().ToDictionary(user => user.Id, user => user.DisplayName);
    }

    private static string ActorName(IReadOnlyDictionary<long, string> names, long userId)
    {
        return names.TryGetValue(userId, out string? name) ? name : DefaultUser.DisplayName;
    }

    private void ShowEmpty(string message)
    {
        EmptyText.Text = message;
        EmptyText.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
        PayrollList.ItemsSource = null;
    }

    public sealed record MonthChoice(int Number, string Name);
}
