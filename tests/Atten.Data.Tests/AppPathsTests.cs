using Xunit;

namespace Atten.Data.Tests;

public class AppPathsTests : IDisposable
{
    public AppPathsTests()
    {
        AppPaths.ResetOverrides();
    }

    public void Dispose()
    {
        AppPaths.ResetOverrides();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AppDirIsProjectRootWhenNotPublished()
    {
        AppPaths.PublishedOverride = () => false;
        AppPaths.AppDirOverride = null;

        string expected = AppPaths.FindRepoRoot([AppContext.BaseDirectory, Directory.GetCurrentDirectory()])
            ?? throw new InvalidOperationException("Test host is not under the repository.");

        Assert.Equal(expected, AppPaths.AppDir());
        Assert.True(File.Exists(Path.Combine(AppPaths.AppDir(), "Atten.sln")));
    }

    [Fact]
    public void PublishedAppDirIsBesideTheExecutable()
    {
        string folder = CreateTempDirectory();
        try
        {
            string exe = Path.Combine(folder, "Atten.App.exe");
            File.WriteAllBytes(exe, []);
            AppPaths.PublishedOverride = () => true;
            AppPaths.ProcessPathOverride = exe;

            Assert.Equal(Path.GetFullPath(folder), AppPaths.AppDir());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void DbFileIsUnderDataDir()
    {
        string folder = CreateTempDirectory();
        try
        {
            AppPaths.AppDirOverride = () => folder;

            Assert.Equal(Path.Combine(folder, "data", "atten.db"), AppPaths.DbFile());
            Assert.True(Directory.Exists(Path.Combine(folder, "data")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void StoreOpenWithoutAPathUsesAppPaths()
    {
        string folder = CreateTempDirectory();
        try
        {
            AppPaths.AppDirOverride = () => folder;
            Migrator.Apply();
            IAttenRepository store = AttenStore.Open();
            store.Personnel.Add("علی", "رضایی", 8);
            Assert.Single(store.Personnel.List());
            Assert.True(File.Exists(AppPaths.DbFile()));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string folder = Path.Combine(Path.GetTempPath(), "atten-paths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
