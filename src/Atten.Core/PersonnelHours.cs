using System.Globalization;

namespace Atten.Core;

/// <summary>
/// Daily work length as hours plus minutes. Screens display the formatted
/// value; they do not convert the duration themselves.
/// </summary>
public static class PersonnelHours
{
    public const string MissingDuration = "میزان ساعت کاری را وارد کنید.";
    public const string InvalidNumber = "ساعت و دقیقه را با عدد وارد کنید.";
    public const string MinutesRange = "دقیقه نمی‌تواند بیشتر از ۵۹ باشد.";
    public const string MustBePositive = "میزان ساعت کاری باید بیشتر از صفر باشد.";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static (int Hours, int Minutes) Split(double dailyHours)
    {
        int totalMinutes = (int)Math.Round(dailyHours * 60);
        if (totalMinutes < 0)
        {
            totalMinutes = 0;
        }

        return Math.DivRem(totalMinutes, 60);
    }

    public static string Format(double dailyHours)
    {
        if (!double.IsFinite(dailyHours))
        {
            return string.Empty;
        }

        if (Math.Abs(dailyHours - Math.Round(dailyHours)) < 0.0005)
        {
            return Math.Round(dailyHours).ToString("0", Invariant);
        }

        return dailyHours.ToString("0.##", Invariant);
    }

    public static double Parse(string? hoursText, string? minutesText)
    {
        string hoursRaw = hoursText?.Trim() ?? string.Empty;
        string minutesRaw = minutesText?.Trim() ?? string.Empty;
        if (hoursRaw.Length == 0 && minutesRaw.Length == 0)
        {
            throw new ArgumentException(MissingDuration);
        }

        int? hours = hoursRaw.Length == 0 ? 0 : WholeNumber(hoursRaw);
        int? minutes = minutesRaw.Length == 0 ? 0 : WholeNumber(minutesRaw);
        if (hours is null || minutes is null)
        {
            throw new ArgumentException(InvalidNumber);
        }

        if (minutes > 59)
        {
            throw new ArgumentException(MinutesRange);
        }

        double total = hours.Value + (minutes.Value / 60.0);
        if (total <= 0)
        {
            throw new ArgumentException(MustBePositive);
        }

        return total;
    }

    public static bool AcceptsHours(string proposed)
    {
        return proposed.Length == 0 || WholeNumber(proposed) is not null;
    }

    public static bool AcceptsMinutes(string proposed)
    {
        if (proposed.Length == 0)
        {
            return true;
        }

        int? value = WholeNumber(proposed);
        return value is not null && value <= 59;
    }

    public static int? WholeNumber(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string cleaned = Persian.EnglishDigits(text.Trim());
        if (cleaned.Length == 0)
        {
            return null;
        }

        foreach (char c in cleaned)
        {
            if (c is < '0' or > '9')
            {
                return null;
            }
        }

        return int.Parse(cleaned, Invariant);
    }
}
