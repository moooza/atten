namespace Atten.Core;

/// <summary>
/// Pair device punches with manual times and compare them to daily hours.
/// Manual times and the balance live outside clock events.
/// Approved leave fills a shortfall on the working days it covers.
/// A balance is hours and minutes: -2.20 is minus 2 hours and 20 minutes.
/// </summary>
public static class Sheet
{
    private const string InvalidTime = "ساعت معتبر نیست.";
    private const string InvalidRange = "تاریخ پایان باید بعد از تاریخ شروع یا برابر با آن باشد.";

    public static string? ParseClock(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string cleaned = Persian.EnglishDigits(text.Trim());
        if (cleaned.Length == 0)
        {
            return null;
        }

        string[] pieces = cleaned.Split(':');
        if (pieces.Length is not (2 or 3) || !pieces.All(IsAsciiDigits))
        {
            throw new FormatException(InvalidTime);
        }

        int hour = int.Parse(pieces[0], System.Globalization.CultureInfo.InvariantCulture);
        int minute = int.Parse(pieces[1], System.Globalization.CultureInfo.InvariantCulture);
        int second = pieces.Length == 3
            ? int.Parse(pieces[2], System.Globalization.CultureInfo.InvariantCulture)
            : 0;
        if (hour > 23 || minute > 59 || second > 59)
        {
            throw new FormatException(InvalidTime);
        }

        return $"{hour:00}:{minute:00}:{second:00}";
    }

    public static string FormatBalance(int minutes)
    {
        string sign = minutes > 0 ? "+" : minutes < 0 ? "-" : "";
        int hours = Math.DivRem(Math.Abs(minutes), 60, out int mins);
        return $"{sign}{hours}.{mins:00}";
    }

    public static int RequiredMinutes(double dailyHours)
    {
        return (int)Math.Round(dailyHours * 60);
    }

    public static IReadOnlyList<SheetRow> BuildSheet(
        DateOnly start,
        DateOnly end,
        IEnumerable<DeviceDay> deviceRows,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, string?>>? overrides,
        double dailyHours,
        IReadOnlyDictionary<string, bool>? holidays = null,
        IEnumerable<LeaveSpan>? leaves = null)
    {
        ArgumentNullException.ThrowIfNull(deviceRows);

        string startKey = Dates.StorageDate(start);
        string endKey = Dates.StorageDate(end);
        if (string.CompareOrdinal(startKey, endKey) > 0)
        {
            throw new ArgumentException(InvalidRange);
        }

        var device = new Dictionary<string, string[]>();
        foreach (DeviceDay row in deviceRows)
        {
            device[row.Date] = row.Times.Select(value => value).ToArray();
        }

        IReadOnlyDictionary<string, bool> chosen = holidays ?? new Dictionary<string, bool>();
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, string?>> chosenOverrides =
            overrides ?? new Dictionary<string, IReadOnlyDictionary<int, string?>>();
        int expected = RequiredMinutes(dailyHours);
        LeaveSpan[] recorded = leaves?.ToArray() ?? [];
        IReadOnlyDictionary<string, int> leaveCredit = Leave.SpreadLeaveMinutes(
            recorded,
            day => IsHoliday(day, chosen));

        var rows = new List<SheetRow>();
        for (DateOnly current = start; string.CompareOrdinal(Dates.StorageDate(current), endKey) <= 0; current = current.AddDays(1))
        {
            string key = Dates.StorageDate(current);
            string?[] slots = DaySlots(
                device.GetValueOrDefault(key, []),
                chosenOverrides.GetValueOrDefault(key));
            bool holiday = IsHoliday(current, chosen);
            int required = holiday ? 0 : expected;
            int worked = WorkedMinutes(slots);
            int credited = leaveCredit.GetValueOrDefault(key);
            int credit = Math.Min(credited, Math.Max(0, required - worked));
            bool absent = !slots.Any(slot => slot is not null);
            rows.Add(new SheetRow(
                key,
                slots,
                absent && credit >= required ? false : Incomplete(slots, holiday),
                worked + credit - required,
                holiday,
                credited,
                OverlapsLeave(key, recorded)));
        }

        return rows;
    }

    private static bool IsHoliday(DateOnly day, IReadOnlyDictionary<string, bool> chosen)
    {
        string key = Dates.StorageDate(day);
        if (chosen.TryGetValue(key, out bool holiday))
        {
            return holiday;
        }

        return day.DayOfWeek == DayOfWeek.Friday;
    }

    private static bool OverlapsLeave(string dayKey, IReadOnlyList<LeaveSpan> leaves)
    {
        foreach (LeaveSpan leave in leaves)
        {
            string start = Dates.StorageDate(leave.StartDate);
            string end = Dates.StorageDate(leave.EndDate);
            if (string.CompareOrdinal(start, dayKey) <= 0 && string.CompareOrdinal(dayKey, end) <= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string?[] DaySlots(IReadOnlyList<string> device, IReadOnlyDictionary<int, string?>? overrides)
    {
        int count = device.Count;
        if (count == 0)
        {
            count = 2;
        }
        else if (count % 2 == 1)
        {
            count += 1;
        }

        if (overrides is { Count: > 0 })
        {
            count = Math.Max(count, overrides.Keys.Max() + 1);
        }

        if (count % 2 == 1)
        {
            count += 1;
        }

        var slots = new string?[count];
        for (int index = 0; index < count; index++)
        {
            if (overrides is not null && overrides.TryGetValue(index, out string? value))
            {
                slots[index] = value;
            }
            else if (index < device.Count)
            {
                slots[index] = device[index];
            }
            else
            {
                slots[index] = null;
            }
        }

        return slots;
    }

    private static bool Incomplete(IReadOnlyList<string?> slots, bool holiday)
    {
        if (holiday && !slots.Any(slot => slot is not null))
        {
            return false;
        }

        if (slots.Count == 0)
        {
            return true;
        }

        for (int index = 0; index < slots.Count; index += 2)
        {
            string? entry = slots[index];
            string? exitTime = index + 1 < slots.Count ? slots[index + 1] : null;
            if (string.IsNullOrEmpty(entry) || string.IsNullOrEmpty(exitTime))
            {
                return true;
            }

            if (ClockSeconds(exitTime) <= ClockSeconds(entry))
            {
                return true;
            }
        }

        return false;
    }

    private static int WorkedMinutes(IReadOnlyList<string?> slots)
    {
        int totalSeconds = 0;
        for (int index = 0; index < slots.Count - 1; index += 2)
        {
            string? entry = slots[index];
            string? exitTime = slots[index + 1];
            if (string.IsNullOrEmpty(entry) || string.IsNullOrEmpty(exitTime))
            {
                continue;
            }

            int start = ClockSeconds(entry);
            int end = ClockSeconds(exitTime);
            if (end <= start)
            {
                continue;
            }

            totalSeconds += end - start;
        }

        return totalSeconds / 60;
    }

    private static int ClockSeconds(string value)
    {
        string[] parts = value.Split(':');
        int hour = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        int minute = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        int second = int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
        return hour * 3600 + minute * 60 + second;
    }

    private static bool IsAsciiDigits(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record DeviceDay(string Date, IReadOnlyList<string> Times);

public sealed record SheetRow(
    string Date,
    IReadOnlyList<string?> Slots,
    bool Incomplete,
    int BalanceMinutes,
    bool Holiday,
    int LeaveMinutes = 0,
    bool OnLeave = false);
