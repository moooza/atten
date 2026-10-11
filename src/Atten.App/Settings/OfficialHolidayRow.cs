using System.ComponentModel;
using Atten.Core;
using Atten.Data;

namespace Atten.App.Settings;

public sealed class OfficialHolidayRow : INotifyPropertyChanged
{
    private bool _chosen;

    public OfficialHolidayRow(OfficialHolidayRecord record)
    {
        Record = record;
        Date = Dates.FormatShamsiDate(record.Date);
        Title = record.Title;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public OfficialHolidayRecord Record { get; }

    public string Date { get; }

    public string Title { get; }

    public bool IsChosen
    {
        get => _chosen;
        set
        {
            if (_chosen == value)
            {
                return;
            }

            _chosen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChosen)));
        }
    }
}
