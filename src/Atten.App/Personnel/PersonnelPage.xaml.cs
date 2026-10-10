using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Atten.App.Personnel;

public sealed partial class PersonnelPage : Page
{
    private readonly IPersonnelRepository? _store;
    private FormWindow? _editorWindow;
    private PersonnelForm? _editor;

    public PersonnelPage()
    {
        InitializeComponent();
        _store = App.Current.Store?.Personnel;
        Loaded += (_, _) => Reload();
        Unloaded += (_, _) => _editorWindow?.Close();
    }

    private void OnAdd(object sender, RoutedEventArgs args)
    {
        Notice.Text = string.Empty;
        if (_store is null)
        {
            Notice.Text = AppMessages.PersonnelListFailed;
            return;
        }

        OpenEditor(null);
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
        if (RowFrom(args.OriginalSource) is not PersonnelRow row)
        {
            return;
        }

        PeopleList.SelectedItem = row;
        Notice.Text = string.Empty;
        var menu = new AppMenuFlyout();
        var edit = new AppMenuItem { Text = "ویرایش" };
        edit.Click += (_, _) => OpenEditor(row.Record);
        menu.Items.Add(edit);
        if (args.OriginalSource is FrameworkElement target)
        {
            menu.ShowAt(target, args.GetPosition(target));
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        Notice.Text = string.Empty;
    }

    private void OpenSelected()
    {
        if (_store is null)
        {
            Notice.Text = AppMessages.PersonnelListFailed;
            return;
        }

        if (PeopleList.SelectedItem is not PersonnelRow row)
        {
            Notice.Text = AppMessages.PersonnelSelectOne;
            return;
        }

        OpenEditor(row.Record);
    }

    private void OpenEditor(PersonnelRecord? person)
    {
        if (_store is null)
        {
            return;
        }

        string title = person is null ? AppMessages.PersonnelAdd : AppMessages.PersonnelEdit;
        if (_editorWindow is null || _editor is null)
        {
            _editor = new PersonnelForm();
            _editor.Saved += OnEditorSaved;
            _editor.Cancelled += OnEditorCancelled;
            _editorWindow = new FormWindow(title, _editor, new SizeInt32(480, 720));
            _editorWindow.Closed += OnEditorClosed;
        }
        else
        {
            _editorWindow.Title = title;
        }

        _editor.Bind(_store, person);
        _editorWindow.BringForward();
    }

    private static PersonnelRow? RowFrom(object? source)
    {
        DependencyObject? current = source as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: PersonnelRow row })
            {
                return row;
            }

            if (current is ListViewItem { Content: PersonnelRow item })
            {
                return item;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void OnEditorSaved(object? sender, EventArgs args)
    {
        _editorWindow?.Close();
        Reload();
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

    private void Reload()
    {
        Notice.Text = string.Empty;
        if (_store is null)
        {
            CountText.Text = string.Empty;
            ShowEmpty(AppMessages.PersonnelListFailed);
            return;
        }

        try
        {
            IReadOnlyList<PersonnelRow> rows = _store.List().Select(person => new PersonnelRow(person)).ToList();
            CountText.Text = $"{rows.Count} نفر";
            PeopleList.ItemsSource = rows;
            if (rows.Count == 0)
            {
                ShowEmpty(AppMessages.PersonnelEmpty);
                return;
            }

            EmptyText.Visibility = Visibility.Collapsed;
            TablePanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            CountText.Text = string.Empty;
            ShowEmpty($"{AppMessages.PersonnelListFailed}: {ex.Message}");
        }
    }

    private void ShowEmpty(string message)
    {
        EmptyText.Text = message;
        EmptyText.Visibility = Visibility.Visible;
        TablePanel.Visibility = Visibility.Collapsed;
        PeopleList.ItemsSource = null;
    }
}
