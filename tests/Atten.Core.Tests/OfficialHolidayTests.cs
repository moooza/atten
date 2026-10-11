using Xunit;

namespace Atten.Core.Tests;

public class OfficialHolidayTests
{
    private static readonly DateOnly Wednesday = new(2026, 10, 7);
    private static readonly DateOnly Friday = new(2026, 10, 9);
    private static readonly DateOnly Nowruz1405 = Dates.ParseShamsiDate("1405/01/01");

    [Fact]
    public void CatalogLooksUpGregorianStorageDates()
    {
        IReadOnlySet<string> catalog = OfficialHoliday.Catalog([Nowruz1405, Wednesday]);

        Assert.True(OfficialHoliday.Contains(catalog, Nowruz1405));
        Assert.True(OfficialHoliday.Contains(catalog, Dates.StorageDate(Wednesday)));
        Assert.False(OfficialHoliday.Contains(catalog, Friday));
        Assert.False(OfficialHoliday.Contains(catalog, Dates.ParseShamsiDate("1404/01/01")));
        Assert.Equal(1405, Dates.ShamsiYmd(Nowruz1405).Year);
    }

    [Fact]
    public void CatalogAcceptsStoredDateText()
    {
        IReadOnlySet<string> catalog = OfficialHoliday.Catalog([Dates.StorageDate(Nowruz1405)]);

        Assert.True(OfficialHoliday.Contains(catalog, Nowruz1405));
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "2026-03-21" }, catalog);
    }

    [Fact]
    public void DecisionUsesPersonOverrideThenCatalogThenFriday()
    {
        IReadOnlySet<string> catalog = OfficialHoliday.Catalog([Nowruz1405, Wednesday]);

        Assert.True(OfficialHoliday.IsHoliday(Nowruz1405, catalog));
        Assert.True(OfficialHoliday.IsHoliday(Wednesday, catalog));
        Assert.True(OfficialHoliday.IsHoliday(Friday, catalog));
        Assert.False(OfficialHoliday.IsHoliday(new DateOnly(2026, 10, 8), catalog));

        Assert.False(OfficialHoliday.IsHoliday(Friday, catalog, personOverride: false));
        Assert.False(OfficialHoliday.IsHoliday(Wednesday, catalog, personOverride: false));
        Assert.True(OfficialHoliday.IsHoliday(new DateOnly(2026, 10, 8), catalog, personOverride: true));
    }

    [Fact]
    public void MissingCatalogKeepsOnlyTheFridayRule()
    {
        Assert.False(OfficialHoliday.IsHoliday(Wednesday));
        Assert.True(OfficialHoliday.IsHoliday(Friday));
        Assert.False(OfficialHoliday.IsHoliday(Nowruz1405));
        Assert.True(OfficialHoliday.IsHoliday(Friday, official: OfficialHoliday.Catalog(Array.Empty<string>())));
    }
}
