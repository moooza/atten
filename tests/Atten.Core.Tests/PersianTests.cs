using Xunit;

namespace Atten.Core.Tests;

public class PersianTests
{
    [Fact]
    public void ArabicYehAndKafBecomePersian()
    {
        Assert.Equal("عل\u06cc", Persian.PersianLetters("عل\u064a"));
        Assert.Equal("\u06a9اظم\u06cc", Persian.PersianLetters("\u0643اظم\u0649"));
        Assert.Equal("\u06a9\u062a\u0627\u0628", Persian.PersianLetters("\u06a9\u062a\u0627\u0628"));
    }

    [Fact]
    public void PersianAndArabicDigitsBecomeEnglish()
    {
        Assert.Equal("0912", Persian.EnglishDigits("۰۹۱۲"));
        Assert.Equal("123", Persian.EnglishDigits("\u0661\u0662\u0663"));
        Assert.Equal("ساعت 8", Persian.EnglishDigits("ساعت ۸"));
    }

    [Fact]
    public void NormalizeTextFixesLettersAndDigitsTogether()
    {
        Assert.Equal("\u06a9\u062f \u06cc12", Persian.NormalizeText("\u06a9\u062f \u064a۱۲"));
    }
}
