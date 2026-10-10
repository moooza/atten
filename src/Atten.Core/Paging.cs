namespace Atten.Core;

/// <summary>
/// Page size choices and 1-based page math. Screens and Theme bind to this;
/// they do not invent their own skip or last-page rules.
/// </summary>
public static class Paging
{
    public static readonly IReadOnlyList<int> PageSizes = [10, 20, 50, 100, 250, 500, 1000];

    public const int DefaultPageSize = 50;

    public static int NormalizeSize(int pageSize)
    {
        return PageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;
    }

    public static int PageCount(int total, int pageSize)
    {
        int size = NormalizeSize(pageSize);
        if (total <= 0)
        {
            return 0;
        }

        return (total + size - 1) / size;
    }

    public static int ClampPage(int page, int total, int pageSize)
    {
        int pages = PageCount(total, pageSize);
        if (pages <= 0)
        {
            return 1;
        }

        if (page < 1)
        {
            return 1;
        }

        return page > pages ? pages : page;
    }

    public static int Skip(int page, int pageSize)
    {
        int size = NormalizeSize(pageSize);
        int safePage = page < 1 ? 1 : page;
        return (safePage - 1) * size;
    }

    public static (int Start, int End) Range(int total, int page, int pageSize)
    {
        if (total <= 0)
        {
            return (0, 0);
        }

        int size = NormalizeSize(pageSize);
        int safePage = ClampPage(page, total, size);
        int start = ((safePage - 1) * size) + 1;
        int end = Math.Min(total, safePage * size);
        return (start, end);
    }
}
