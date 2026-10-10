using Xunit;

namespace Atten.Core.Tests;

public class PagingTests
{
    [Fact]
    public void PageMathClampsAndSkipsFromOne()
    {
        Assert.Equal(50, Paging.NormalizeSize(7));
        Assert.Equal(20, Paging.NormalizeSize(20));
        Assert.Equal(0, Paging.PageCount(0, 50));
        Assert.Equal(3, Paging.PageCount(101, 50));
        Assert.Equal(1, Paging.ClampPage(0, 101, 50));
        Assert.Equal(3, Paging.ClampPage(9, 101, 50));
        Assert.Equal(50, Paging.Skip(2, 50));
        Assert.Equal((51, 100), Paging.Range(101, 2, 50));
        Assert.Equal((101, 101), Paging.Range(101, 3, 50));
        Assert.Equal((0, 0), Paging.Range(0, 1, 50));
    }
}
