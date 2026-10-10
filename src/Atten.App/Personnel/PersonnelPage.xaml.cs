using Atten.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Personnel;

public sealed partial class PersonnelPage : Page
{
    private readonly IPersonnelRepository? _store;

    public PersonnelPage()
    {
        InitializeComponent();
        _store = App.Current.Store?.Personnel;
        Editor.Saved += (_, _) =>
        {
            HideForm();
            Reload();
        };
        Editor.Cancelled += (_, _) => HideForm();
        Loaded += (_, _) => Reload();
    }

    private void OnAdd(object sender, RoutedEventArgs args)
    {
        Notice.Text = string.Empty;
        if (_store is null)
        {
            Notice.Text = AppMessages.PersonnelListFailed;
            return;
        }

        Editor.Bind(_store, null);
        ShowForm();
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

        Editor.Bind(_store, row.Record);
        ShowForm();
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
