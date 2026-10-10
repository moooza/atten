using System.Globalization;
using Atten.Core;
using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Atten.App.Personnel;

public sealed partial class PersonnelForm : UserControl
{
    private IPersonnelRepository? _store;
    private long? _personId;

    public PersonnelForm()
    {
        InitializeComponent();
    }

    public event EventHandler? Saved;

    public event EventHandler? Cancelled;

    public void Bind(IPersonnelRepository store, PersonnelRecord? person)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _personId = person?.Id;
        Heading.Text = person is null ? AppMessages.PersonnelAdd : AppMessages.PersonnelEdit;
        ErrorText.Text = string.Empty;
        FirstNameBox.Text = person?.FirstName ?? string.Empty;
        LastNameBox.Text = person?.LastName ?? string.Empty;
        RemoteIdBox.Text = person?.RemoteId ?? string.Empty;
        MobileBox.Text = person?.Mobile ?? string.Empty;
        if (person is null)
        {
            HoursBox.Text = string.Empty;
            MinutesBox.Text = string.Empty;
            StartField.StorageDate = null;
            StartField.Text = string.Empty;
            EndField.StorageDate = null;
            EndField.Text = string.Empty;
        }
        else
        {
            var (hours, minutes) = PersonnelHours.Split(person.DailyHours);
            HoursBox.Text = hours.ToString(CultureInfo.InvariantCulture);
            MinutesBox.Text = minutes.ToString(CultureInfo.InvariantCulture);
            StartField.StorageDate = person.CooperationStart;
            EndField.StorageDate = person.CooperationEnd;
        }

        FirstNameBox.Focus(FocusState.Programmatic);
    }

    private void OnSave(object sender, RoutedEventArgs args)
    {
        Save();
    }

    private void OnCancel(object sender, RoutedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnEnterAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Save();
        args.Handled = true;
    }

    private void OnEscapeAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        args.Handled = true;
    }

    private void OnHoursChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        args.Cancel = !PersonnelHours.AcceptsHours(args.NewText);
    }

    private void OnMinutesChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        args.Cancel = !PersonnelHours.AcceptsMinutes(args.NewText);
    }

    private void Save()
    {
        if (_store is null)
        {
            return;
        }

        try
        {
            double hours = PersonnelHours.Parse(HoursBox.Text, MinutesBox.Text);
            DateOnly? started = OptionalDate(StartField);
            DateOnly? ended = OptionalDate(EndField);
            if (_personId is null)
            {
                _store.Add(
                    FirstNameBox.Text,
                    LastNameBox.Text,
                    hours,
                    RemoteIdBox.Text,
                    MobileBox.Text,
                    started,
                    ended);
            }
            else
            {
                _store.Update(
                    _personId.Value,
                    FirstNameBox.Text,
                    LastNameBox.Text,
                    hours,
                    RemoteIdBox.Text,
                    MobileBox.Text,
                    started,
                    ended);
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private static DateOnly? OptionalDate(ShamsiDateField field)
    {
        if (string.IsNullOrWhiteSpace(field.Text))
        {
            return null;
        }

        return field.GregorianDate ?? Dates.ParseShamsiDate(field.Text);
    }
}
