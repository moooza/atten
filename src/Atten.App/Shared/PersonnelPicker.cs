using Atten.Data;
using Atten.UI.Theme;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Atten.App.Shared;

/// <summary>
/// Loads personnel through the repository. Leave, calculation, and payroll
/// use this control; they do not open the Personnel window.
/// </summary>
public sealed class PersonnelPicker : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(IPersonnelRepository),
        typeof(PersonnelPicker),
        new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty SelectedPersonnelIdProperty = DependencyProperty.Register(
        nameof(SelectedPersonnelId),
        typeof(object),
        typeof(PersonnelPicker),
        new PropertyMetadata(null, OnSelectedPersonnelIdChanged));

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(PersonnelPicker),
        new PropertyMetadata(AppMessages.ChoosePersonnel, OnPlaceholderTextChanged));

    private readonly AppComboBox _combo;
    private IReadOnlyList<PersonnelRecord> _people = [];
    private bool _syncing;
    private bool _autoBound;

    public PersonnelPicker()
    {
        FlowDirection = FlowDirection.RightToLeft;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinWidth = 220;

        _combo = new AppComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = AppMessages.ChoosePersonnel,
            DisplayMemberPath = nameof(Choice.Label),
            SelectedValuePath = nameof(Choice.Id),
        };
        _combo.SelectionChanged += OnComboSelectionChanged;
        Content = _combo;

        Loaded += OnLoaded;
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler? LoadFailed;

    /// <summary>Personnel list. Pages set this; the picker does not open a database path.</summary>
    public IPersonnelRepository? Source
    {
        get => (IPersonnelRepository?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Selected row id, or null when nothing is chosen.</summary>
    public long? SelectedPersonnelId
    {
        get => AsId(GetValue(SelectedPersonnelIdProperty));
        set => SetValue(SelectedPersonnelIdProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public PersonnelRecord? SelectedPerson
    {
        get
        {
            if (SelectedPersonnelId is not long id)
            {
                return null;
            }

            return _people.FirstOrDefault(person => person.Id == id);
        }
    }

    public IReadOnlyList<PersonnelRecord> People => _people;

    public string? Error { get; private set; }

    public static string FormatLabel(PersonnelRecord person)
    {
        ArgumentNullException.ThrowIfNull(person);
        string name = $"{person.FirstName} {person.LastName}";
        return string.IsNullOrWhiteSpace(person.RemoteId) ? name : $"{name} ({person.RemoteId})";
    }

    public void Reload()
    {
        IPersonnelRepository? source = Source ?? TryStore();
        long? keep = SelectedPersonnelId;
        if (source is null)
        {
            BindPeople([], keep);
            return;
        }

        try
        {
            BindPeople(source.List(), keep);
            Error = null;
        }
        catch (Exception ex)
        {
            BindPeople([], keep);
            Error = $"{AppMessages.PersonnelLoadFailed}: {ex.Message}";
            LoadFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (Source is null && !_autoBound)
        {
            _autoBound = true;
            IPersonnelRepository? store = TryStore();
            if (store is not null)
            {
                Source = store;
                return;
            }
        }

        if (_people.Count == 0 && Source is not null)
        {
            Reload();
        }
    }

    private static IPersonnelRepository? TryStore()
    {
        return Application.Current is App app ? app.Store?.Personnel : null;
    }

    private static long? AsId(object? value)
    {
        return value switch
        {
            long id => id,
            int id => id,
            _ => null,
        };
    }

    private void BindPeople(IReadOnlyList<PersonnelRecord> people, long? keep)
    {
        _people = people;
        var choices = people.Select(person => new Choice(person)).ToList();
        _syncing = true;
        _combo.ItemsSource = choices;
        Choice? match = keep is long id ? choices.FirstOrDefault(item => item.Id == id) : null;
        _combo.SelectedItem = match;
        _syncing = false;
        long? next = match?.Id;
        if (next != SelectedPersonnelId)
        {
            SelectedPersonnelId = next;
        }
    }

    private void OnComboSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        long? next = _combo.SelectedItem is Choice choice ? choice.Id : null;
        if (next == SelectedPersonnelId)
        {
            return;
        }

        _syncing = true;
        SelectedPersonnelId = next;
        _syncing = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((PersonnelPicker)sender).Reload();
    }

    private static void OnSelectedPersonnelIdChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var picker = (PersonnelPicker)sender;
        if (picker._syncing)
        {
            return;
        }

        picker.ApplySelectedId();
        picker.SelectionChanged?.Invoke(picker, EventArgs.Empty);
    }

    private static void OnPlaceholderTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((PersonnelPicker)sender)._combo.PlaceholderText = args.NewValue as string ?? string.Empty;
    }

    private void ApplySelectedId()
    {
        long? id = SelectedPersonnelId;
        _syncing = true;
        if (id is long chosen)
        {
            _combo.SelectedItem = _combo.Items.OfType<Choice>().FirstOrDefault(item => item.Id == chosen);
        }
        else
        {
            _combo.SelectedItem = null;
        }

        _syncing = false;
    }

    private sealed class Choice
    {
        public Choice(PersonnelRecord record)
        {
            Record = record;
            Id = record.Id;
            Label = FormatLabel(record);
        }

        public PersonnelRecord Record { get; }

        public long Id { get; }

        public string Label { get; }
    }
}
