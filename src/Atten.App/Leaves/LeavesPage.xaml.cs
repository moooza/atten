using Atten.App.Shared;
using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Atten.App.Leaves;

public sealed partial class LeavesPage : Page
{
    private readonly ILeaveRepository? _store;
    private readonly IPersonnelRepository? _people;
    private readonly HashSet<int> _checkedYears = [Leave.ShamsiYearOf(DateOnly.FromDateTime(DateTime.Today))];
    private long? _yearsPersonId;
    private FormWindow? _editorWindow;
    private LeaveForm? _editor;

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
        Loaded += (_, _) => Reload(keepYears: false);
        Unloaded += (_, _) => _editorWindow?.Close();
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

    private void OnRowRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (RowFrom(args.OriginalSource) is not LeaveRow row)
        {
            return;
        }

        LeavesList.SelectedItem = row;
        Notice.Text = string.Empty;
        var menu = new AppMenuFlyout();
        var edit = new AppMenuItem { Text = "ویرایش" };
        edit.Click += (_, _) => OpenForm(row.Record.PersonnelId, row.Record);
        var remove = new AppMenuItem { Text = AppMessages.LeaveDelete };
        remove.Click += async (_, _) => await DeleteLeave(row);
        menu.Items.Add(edit);
        menu.Items.Add(remove);
        if (args.OriginalSource is FrameworkElement target)
        {
            menu.ShowAt(target, args.GetPosition(target));
        }
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

        string title = leave is null ? AppMessages.LeaveAdd : AppMessages.LeaveEdit;
        if (_editorWindow is null || _editor is null)
        {
            _editor = new LeaveForm();
            _editor.Saved += OnEditorSaved;
            _editor.Cancelled += OnEditorCancelled;
            _editorWindow = new FormWindow(title, _editor, new SizeInt32(580, 640));
            _editorWindow.Closed += OnEditorClosed;
        }
        else
        {
            _editorWindow.Title = title;
        }

        _editor.Bind(_store, _people, personnelId, leave);
        _editorWindow.BringForward();
    }

    private static LeaveRow? RowFrom(object? source)
    {
        DependencyObject? current = source as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: LeaveRow row })
            {
                return row;
            }

            if (current is ListViewItem { Content: LeaveRow item })
            {
                return item;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private async Task DeleteLeave(LeaveRow row)
    {
        if (_store is null)
        {
            Notice.Text = AppMessages.LeaveListFailed;
            return;
        }

        if (!await ConfirmDelete(row))
        {
            return;
        }

        try
        {
            _store.Delete(row.Record.Id);
            Reload(keepYears: true);
        }
        catch (Exception ex)
        {
            Notice.Text = $"{AppMessages.LeaveDeleteFailed}: {ex.Message}";
        }
    }

    private async Task<bool> ConfirmDelete(LeaveRow row)
    {
        string person = PersonPicker.SelectedPerson is PersonnelRecord chosen
            ? PersonnelPicker.FormatLabel(chosen)
            : AppMessages.ChoosePersonnel;
        string leave = AppMessages.LeaveSpan(row.StartDate, row.EndDate, row.Amount);
        Brush accent = Theme.Brush(this, Tokens.Accent);
        var heading = new TextBlock { Text = AppMessages.LeaveDeleteTitle };
        ApplyTextStyle(heading, "AppTitleText");
        AlignRight(heading);
        var ask = new TextBlock { Text = AppMessages.LeaveDeleteAsk };
        ApplyTextStyle(ask, "AppInkText");
        AlignRight(ask);
        var personLine = HighlightedLine("کارمند: ", person, accent);
        var leaveLine = HighlightedLine("مرخصی: ", leave, accent);
        var cancel = new AppButton
        {
            Content = "انصراف",
            Kind = AppButtonKind.Primary,
        };
        var remove = new AppButton
        {
            Content = AppMessages.LeaveDelete,
            Kind = AppButtonKind.Danger,
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            FlowDirection = FlowDirection.RightToLeft,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
        };
        actions.Children.Add(cancel);
        actions.Children.Add(remove);

        var body = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FlowDirection = FlowDirection.LeftToRight,
        };
        body.Children.Add(ask);
        body.Children.Add(personLine);
        body.Children.Add(leaveLine);
        body.Children.Add(actions);

        var dialog = new ContentDialog
        {
            Title = heading,
            Content = body,
            XamlRoot = XamlRoot,
            FlowDirection = FlowDirection.RightToLeft,
            RequestedTheme = ActualTheme,
        };
        bool confirmed = false;
        cancel.Click += (_, _) => dialog.Hide();
        remove.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Hide();
        };
        await dialog.ShowAsync();
        return confirmed;
    }

    private static TextBlock HighlightedLine(string label, string value, Brush accent)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        ApplyTextStyle(block, "AppInkText");
        AlignRight(block);
        block.Inlines.Add(new Run { Text = label });
        block.Inlines.Add(new Run { Text = value, Foreground = accent });
        return block;
    }

    private static void AlignRight(TextBlock block)
    {
        block.FlowDirection = FlowDirection.LeftToRight;
        block.TextAlignment = TextAlignment.Right;
        block.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private static void ApplyTextStyle(TextBlock block, string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out object? resource) && resource is Style style)
        {
            block.Style = style;
        }
    }

    private void OnEditorSaved(object? sender, EventArgs args)
    {
        _editorWindow?.Close();
        Reload(keepYears: true);
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
}
