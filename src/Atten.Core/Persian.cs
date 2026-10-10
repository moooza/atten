namespace Atten.Core;

/// <summary>
/// Normalize typed Persian before it is stored.
/// Windows Persian keyboards often emit Arabic yeh and kaf. Stored text uses the
/// Persian letters, and stored digits are ASCII.
/// </summary>
public static class Persian
{
    public static string PersianLetters(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value
            .Replace('\u064a', '\u06cc')
            .Replace('\u0649', '\u06cc')
            .Replace('\u0643', '\u06a9');
    }

    public static string EnglishDigits(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c is >= '\u06f0' and <= '\u06f9')
            {
                chars[i] = (char)('0' + (c - '\u06f0'));
            }
            else if (c is >= '\u0660' and <= '\u0669')
            {
                chars[i] = (char)('0' + (c - '\u0660'));
            }
        }

        return new string(chars);
    }

    public static string NormalizeText(string value)
    {
        return EnglishDigits(PersianLetters(value));
    }
}
