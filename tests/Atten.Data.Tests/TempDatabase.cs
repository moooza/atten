namespace Atten.Data.Tests;

internal sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        DatabaseBackup.KeepSnapshots = DatabaseBackup.DefaultKeepSnapshots;
        Folder = Path.Combine(Path.GetTempPath(), "atten-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        DbFile = Path.Combine(Folder, "atten.db");
    }

    public string Folder { get; }

    public string DbFile { get; }

    public IAttenRepository OpenStore(long actorId = DefaultUser.Id)
    {
        return AttenStore.Start(DbFile, actorId);
    }

    public void Dispose()
    {
        DatabaseBackup.KeepSnapshots = DatabaseBackup.DefaultKeepSnapshots;
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
