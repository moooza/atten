namespace Atten.Data;

/// <summary>
/// Writable paths beside the published executable, or at the repo root in development.
/// </summary>
public static class AppPaths
{
    internal static Func<bool>? PublishedOverride { get; set; }
    internal static string? ProcessPathOverride { get; set; }
    internal static Func<string>? AppDirOverride { get; set; }

    public const string DatabaseFileName = "atten.db";
    public const string DataFolderName = "data";

    public static bool IsPublished()
    {
        if (PublishedOverride is not null)
        {
            return PublishedOverride();
        }

        return FindRepoRoot(ProbeDirectories()) is null;
    }

    public static string AppDir()
    {
        if (AppDirOverride is not null)
        {
            return AppDirOverride();
        }

        if (!IsPublished())
        {
            return FindRepoRoot(ProbeDirectories())
                ?? throw new InvalidOperationException("Could not find the repository root.");
        }

        return PublishedAppDir();
    }

    public static string DataDir()
    {
        string folder = Path.Combine(AppDir(), DataFolderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string DbFile()
    {
        return Path.Combine(DataDir(), DatabaseFileName);
    }

    internal static void ResetOverrides()
    {
        PublishedOverride = null;
        ProcessPathOverride = null;
        AppDirOverride = null;
    }

    private static string PublishedAppDir()
    {
        string? processPath = ProcessPathOverride ?? Environment.ProcessPath;
        string? directory = Path.GetDirectoryName(processPath);
        if (!string.IsNullOrEmpty(directory))
        {
            return Path.GetFullPath(directory);
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    private static IEnumerable<string> ProbeDirectories()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
    }

    internal static string? FindRepoRoot(IEnumerable<string> starts)
    {
        foreach (string start in starts)
        {
            string? found = FindRepoRoot(start);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    internal static string? FindRepoRoot(string start)
    {
        DirectoryInfo? current = new(Path.GetFullPath(start));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Atten.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
