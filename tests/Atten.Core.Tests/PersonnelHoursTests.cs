using Xunit;

namespace Atten.Core.Tests;

public class PersonnelHoursTests
{
    [Fact]
    public void SplitTurnsHoursIntoHoursAndMinutes()
    {
        Assert.Equal((4, 30), PersonnelHours.Split(4.5));
        Assert.Equal((8, 0), PersonnelHours.Split(8));
        Assert.Equal((0, 0), PersonnelHours.Split(-1));
    }

    [Fact]
    public void FormatDropsTrailingZeros()
    {
        Assert.Equal("8", PersonnelHours.Format(8));
        Assert.Equal("4.5", PersonnelHours.Format(4.5));
        Assert.Equal("4.25", PersonnelHours.Format(4.25));
        Assert.Equal("0", PersonnelHours.Format(0));
    }

    [Fact]
    public void ParseReadsHoursMinutesAndPersianDigits()
    {
        Assert.Equal(8, PersonnelHours.Parse("8", ""));
        Assert.Equal(0.5, PersonnelHours.Parse("", "30"));
        Assert.Equal(4.5, PersonnelHours.Parse("4", "30"));
        Assert.Equal(4.5, PersonnelHours.Parse("۴", "۳۰"));
    }

    [Fact]
    public void ParseRejectsBlankZeroAndOutOfRangeMinutes()
    {
        ArgumentException missing = Assert.Throws<ArgumentException>(() => PersonnelHours.Parse("", "  "));
        Assert.Equal(PersonnelHours.MissingDuration, missing.Message);

        ArgumentException invalid = Assert.Throws<ArgumentException>(() => PersonnelHours.Parse("ab", "0"));
        Assert.Equal(PersonnelHours.InvalidNumber, invalid.Message);

        ArgumentException minutes = Assert.Throws<ArgumentException>(() => PersonnelHours.Parse("1", "60"));
        Assert.Equal(PersonnelHours.MinutesRange, minutes.Message);

        ArgumentException zero = Assert.Throws<ArgumentException>(() => PersonnelHours.Parse("0", "0"));
        Assert.Equal(PersonnelHours.MustBePositive, zero.Message);
    }

    [Fact]
    public void KeystrokeFiltersAcceptWholeHoursAndMinutesTo59()
    {
        Assert.True(PersonnelHours.AcceptsHours(""));
        Assert.True(PersonnelHours.AcceptsHours("12"));
        Assert.True(PersonnelHours.AcceptsHours("۱۲"));
        Assert.False(PersonnelHours.AcceptsHours("12a"));
        Assert.True(PersonnelHours.AcceptsMinutes(""));
        Assert.True(PersonnelHours.AcceptsMinutes("59"));
        Assert.False(PersonnelHours.AcceptsMinutes("60"));
    }
}
