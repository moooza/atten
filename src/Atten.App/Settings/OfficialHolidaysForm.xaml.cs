using System.ComponentModel;
using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Atten.App.Settings;

public sealed partial class OfficialHolidaysForm : UserControl
{
    private const int YearsBefore = 5;
    private const int YearsAfter = 2;

    private readonly IOfficialHolidayRepository? _store;
    private readonly List<OfficialHolidayRow> _rows = [];
    private bool _bindingYear;
    private bool _syncingChecks;
    private FormWindow? _editorWindow;
    private OfficialHolidayForm? _editor;

    public OfficialHolidaysForm()
    {
        InitializeComponent();
        _store = App.Current.Store?.OfficialHolidays;
        Heading.Text = AppMessages.OfficialHolidays;
        AddButton.Content = AppMessages.OfficialHolidayAdd;
        EditButton.Content = "ویرایش";
        BulkActions.Bind([AppBulkCommand.Delete()]);
        BulkActions.CommandChosen += OnBulkCommand;
        SelectAllBox.ChosenChanged += OnSelectAllChanged;
        YearCombo.SelectionChanged += OnYearChanged;
        Loaded += OnLoaded;
        Unloaded += (_, _) => _editorWindow?.Close();
    }

    public event EventHandler? Cancelled;

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        BindYears();
        Load();
    }

    private void OnAdd(object sender, RoutedEventArgs args)
    {
        Notice.Text = string.Empty;
        OpenEditor(null);
    }

    private void OnEdit(object sender, RoutedEventArgs args)
    {
        OpenSelected();
    }

    private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        if (args.OriginalSource is FrameworkElement source && IsOnSelectBox(source))
        {
            return;
        }

        OpenSelected();
    }

    private void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        args.Handled = true;
    }

    private void OnYearChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_bindingYear)
        {
            return;
        }

        Notice.Text = string.Empty;
        Load();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (Notice.Text == AppMessages.OfficialHolidaySelectOne)
        {
            Notice.Text = string.Empty;
        }
    }

    private void OnRowRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (RowFrom(args.OriginalSource) is not OfficialHolidayRow row)
        {
            return;
        }

        HolidayList.SelectedItem = row;
        Notice.Text = string.Empty;
        var menu = new AppMenuFlyout();
        var edit = new AppMenuItem { Text = "ویرایش" };
        edit.Click += (_, _) => OpenEditor(row.Record);
        var remove = new AppMenuItem { Text = AppMessages.OfficialHolidayDelete };
        remove.Click += async (_, _) => await DeleteHolidays([row]);
        menu.Items.Add(edit);
        menu.Items.Add(remove);
        if (args.OriginalSource is FrameworkElement target)
        {
            menu.ShowAt(target, args.GetPosition(target));
        }
    }

    private void OnBulkCommand(object? sender, AppBulkCommand command)
    {
        if (command.Role != AppBulkKind.Delete)
        {
            return;
        }

        IReadOnlyList<OfficialHolidayRow> chosen = ChosenRows();
        if (chosen.Count == 0)
        {
            Notice.Text = AppMessages.OfficialHolidaySelectNone;
            return;
        }

        Notice.Text = string.Empty;
        _ = DeleteHolidays(chosen);
    }

    private void OnSelectAllChanged(object? sender, EventArgs args)
    {
        if (_syncingChecks)
        {
            return;
        }

        bool chosen = SelectAllBox.IsChosen;
        _syncingChecks = true;
        foreach (OfficialHolidayRow row in _rows)
        {
            row.IsChosen = chosen;
        }

        _syncingChecks = false;
        PaintSelectAll();
    }

    private void OnRowChosenChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(OfficialHolidayRow.IsChosen) || _syncingChecks)
        {
            return;
        }

        PaintSelectAll();
    }

    private void OpenSelected()
    {
        if (HolidayList.SelectedItem is not OfficialHolidayRow row)
        {
            Notice.Text = AppMessages.OfficialHolidaySelectOne;
            return;
        }

        Notice.Text = string.Empty;
        OpenEditor(row.Record);
    }

    private void OpenEditor(OfficialHolidayRecord? holiday)
    {
        if (_store is null)
        {
            Notice.Text = AppMessages.OfficialHolidayListFailed;
            return;
        }

        string title = holiday is null ? AppMessages.OfficialHolidayAdd : AppMessages.OfficialHolidayEdit;
        if (_editorWindow is null || _editor is null)
        {
            _editor = new OfficialHolidayForm();
            _editor.Saved += OnEditorSaved;
            _editor.Cancelled += OnEditorCancelled;
            _editorWindow = new FormWindow(title, _editor, new SizeInt32(520, 620));
            _editorWindow.Closed += OnEditorClosed;
        }
        else
        {
            _editorWindow.Title = title;
        }

        _editor.Bind(_store, holiday);
        _editorWindow.BringForward();
    }

    private void OnEditorSaved(object? sender, EventArgs args)
    {
        if (_editor?.LastSavedDate is DateOnly day)
        {
            RevealYear(Leave.ShamsiYearOf(day));
        }

        _editorWindow?.Close();
        App.Current.ApplyOfficialHolidayCatalog();
        Load();
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

    private void BindYears()
    {
        _bindingYear = true;
        int current = Leave.ShamsiYearOf(DateOnly.FromDateTime(DateTime.Today));
        var years = Enumerable.Range(current - YearsBefore, YearsBefore + YearsAfter + 1)
            .Reverse()
            .ToList();
        int selected = YearCombo.SelectedItem is int year && years.Contains(year) ? year : current;
        YearCombo.ItemsSource = years;
        YearCombo.SelectedItem = years.Contains(selected) ? selected : years.FirstOrDefault();
        _bindingYear = false;
    }

    private void RevealYear(int year)
    {
        var years = (YearCombo.ItemsSource as IEnumerable<int>)?.ToList() ?? [];
        if (!years.Contains(year))
        {
            years.Add(year);
            years = years.Distinct().OrderByDescending(item => item).ToList();
            _bindingYear = true;
            YearCombo.ItemsSource = years;
            _bindingYear = false;
        }

        if (!Equals(YearCombo.SelectedItem, year))
        {
            _bindingYear = true;
            YearCombo.SelectedItem = year;
            _bindingYear = false;
        }
    }

    private async Task DeleteHolidays(IReadOnlyList<OfficialHolidayRow> rows)
    {
        if (_store is null)
        {
            Notice.Text = AppMessages.OfficialHolidayListFailed;
            return;
        }

        if (!await ConfirmDelete(rows))
        {
            return;
        }

        try
        {
            if (rows.Count == 1)
            {
                _store.Delete(rows[0].Record.Id);
            }
            else
            {
                _store.DeleteMany(rows.Select(row => row.Record.Id).ToList());
            }

            Notice.Text = string.Empty;
            App.Current.ApplyOfficialHolidayCatalog();
            Load();
        }
        catch (Exception ex)
        {
            Notice.Text = $"{AppMessages.OfficialHolidayDeleteFailed}: {ex.Message}";
        }
    }

    private Task<bool> ConfirmDelete(IReadOnlyList<OfficialHolidayRow> rows)
    {
        bool many = rows.Count > 1;
        var lines = new List<AppConfirmLine>
        {
            new("تعداد: ", AppMessages.OfficialHolidayCount(rows.Count)),
        };
        foreach (OfficialHolidayRow row in rows.Take(8))
        {
            lines.Add(new AppConfirmLine(string.Empty, $"{row.Date} — {row.Title}"));
        }

        if (rows.Count > 8)
        {
            lines.Add(new AppConfirmLine(string.Empty, "…"));
        }

        return AppConfirmDialog.ShowAsync(
            XamlRoot,
            ActualTheme,
            new AppConfirmRequest
            {
                Title = many ? AppMessages.OfficialHolidayDeleteManyTitle : AppMessages.OfficialHolidayDeleteTitle,
                Ask = many ? AppMessages.OfficialHolidayDeleteManyAsk : AppMessages.OfficialHolidayDeleteAsk,
                Lines = lines,
                ConfirmText = AppMessages.OfficialHolidayDelete,
            });
    }

    private void Load()
    {
        DetachRows();
        if (YearCombo.SelectedItem is not int year)
        {
            CountText.Text = string.Empty;
            ShowEmpty(AppMessages.OfficialHolidayChooseYear);
            return;
        }

        if (_store is null)
        {
            CountText.Text = string.Empty;
            ShowEmpty(AppMessages.OfficialHolidayListFailed);
            return;
        }

        try
        {
            var (start, end) = Leave.ShamsiYearSpan(year);
            _rows.AddRange(_store.List(start, end).Select(row => new OfficialHolidayRow(row)));
            foreach (OfficialHolidayRow row in _rows)
            {
                row.PropertyChanged += OnRowChosenChanged;
            }

            HolidayList.ItemsSource = _rows.ToList();
            PaintSelectAll();
            if (_rows.Count == 0)
            {
                CountText.Text = string.Empty;
                ShowEmpty(AppMessages.OfficialHolidayEmpty);
                return;
            }

            CountText.Text = AppMessages.OfficialHolidayCount(_rows.Count);
            EmptyText.Visibility = Visibility.Collapsed;
            TablePanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            CountText.Text = string.Empty;
            ShowEmpty($"{AppMessages.OfficialHolidayListFailed}: {ex.Message}");
        }
    }

    private void ShowEmpty(string message)
    {
        EmptyText.Text = message;
        EmptyText.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
        HolidayList.ItemsSource = null;
        PaintSelectAll();
    }

    private void DetachRows()
    {
        foreach (OfficialHolidayRow row in _rows)
        {
            row.PropertyChanged -= OnRowChosenChanged;
        }

        _rows.Clear();
    }

    private IReadOnlyList<OfficialHolidayRow> ChosenRows()
    {
        return _rows.Where(row => row.IsChosen).ToList();
    }

    private void PaintSelectAll()
    {
        int chosen = _rows.Count(row => row.IsChosen);
        _syncingChecks = true;
        if (_rows.Count == 0 || chosen == 0)
        {
            SelectAllBox.CheckState = false;
        }
        else if (chosen == _rows.Count)
        {
            SelectAllBox.CheckState = true;
        }
        else
        {
            SelectAllBox.CheckState = null;
        }

        _syncingChecks = false;
    }

    private static bool IsOnSelectBox(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is AppSelectBox)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static OfficialHolidayRow? RowFrom(object? source)
    {
        DependencyObject? current = source as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: OfficialHolidayRow row })
            {
                return row;
            }

            if (current is ListViewItem { Content: OfficialHolidayRow item })
            {
                return item;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
