using System.Text;

namespace Atten.Core;

/// <summary>
/// Read a tab-separated attendance log.
/// Column 1 is the personnel remote id, column 2 is the name, and column 3 is a
/// Gregorian date and clock time separated by a space. Later columns are ignored.
/// File paths stay out of Core; callers pass the file text or bytes.
/// </summary>
public static class Attlog
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Encoding Windows1256 = CreateWindows1256();

    public static string Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            string text = Utf8.GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
        }
        catch (DecoderFallbackException)
        {
            return Windows1256.GetString(bytes);
        }
    }

    public static IReadOnlyList<AttlogRow> Parse(byte[] bytes)
    {
        return Parse(Decode(bytes));
    }

    public static IReadOnlyList<AttlogRow> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var rows = new List<AttlogRow>();
        string[] lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string raw = lines[index];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string[] parts = raw.Split('\t');
            if (parts.Length < 3)
            {
                throw new FormatException($"سطر {lineNumber} ناقص است.");
            }

            string clock = parts[2].Trim();
            int separator = clock.IndexOf(' ');
            if (separator <= 0 || separator == clock.Length - 1)
            {
                throw new FormatException($"سطر {lineNumber} تاریخ یا ساعت ندارد.");
            }

            string eventDate = clock[..separator];
            string eventTime = clock[(separator + 1)..];
            if (eventDate.Length == 0 || eventTime.Length == 0)
            {
                throw new FormatException($"سطر {lineNumber} تاریخ یا ساعت ندارد.");
            }

            rows.Add(new AttlogRow(
                lineNumber,
                parts[0].Trim(),
                parts[1].Trim(),
                eventDate,
                eventTime));
        }

        if (rows.Count == 0)
        {
            throw new FormatException("فایل ردیفی ندارد.");
        }

        return rows;
    }

    private static Encoding CreateWindows1256()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1256);
    }
}

public sealed record AttlogRow(
    int LineNumber,
    string RemoteId,
    string Name,
    string EventDate,
    string EventTime);
