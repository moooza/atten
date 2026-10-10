using System.Text;
using Xunit;

namespace Atten.Core.Tests;

public class AttlogTests
{
    private const string Log =
        "1002\tKarimi\t2026-09-23 20:11:58\t1\t0\n" +
        "\n" +
        "1008\tjamalian\t2026-10-06 18:03:50\t1\t1\n" +
        "1002\tKarimi\t2026-09-23 20:11:58\t1\t0\n";

    [Fact]
    public void ParseKeepsRemoteIdNameAndSplitsTheClock()
    {
        IReadOnlyList<AttlogRow> rows = Attlog.Parse(Log);
        Assert.Equal(
            [
                (1, "1002", "Karimi", "2026-09-23", "20:11:58"),
                (3, "1008", "jamalian", "2026-10-06", "18:03:50"),
                (4, "1002", "Karimi", "2026-09-23", "20:11:58"),
            ],
            rows.Select(row => (row.LineNumber, row.RemoteId, row.Name, row.EventDate, row.EventTime)));
    }

    [Fact]
    public void ParseRejectsAShortLine()
    {
        FormatException error = Assert.Throws<FormatException>(() => Attlog.Parse("1002\tKarimi\n"));
        Assert.Equal("سطر 1 ناقص است.", error.Message);
    }

    [Fact]
    public void DecodeReadsUtf8AndStripsABom()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("1002\tKarimi\t2026-09-23 20:11:58\t1\t0\n")];
        IReadOnlyList<AttlogRow> rows = Attlog.Parse(bom);
        Assert.Equal("1002", rows[0].RemoteId);
        Assert.Equal("Karimi", rows[0].Name);
    }

    [Fact]
    public void DecodeFallsBackToWindows1256()
    {
        byte[] bytes =
        [
            .. "1002\t"u8.ToArray(),
            0xDA, 0xE1, 0xED,
            .. "\t2026-09-23 20:11:58\t1\t0\n"u8.ToArray(),
        ];
        Assert.Throws<DecoderFallbackException>(() => new UTF8Encoding(false, true).GetString(bytes));
        IReadOnlyList<AttlogRow> rows = Attlog.Parse(bytes);
        Assert.Equal("علي", rows[0].Name);
    }
}
