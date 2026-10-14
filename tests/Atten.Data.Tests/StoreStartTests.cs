using Xunit;

namespace Atten.Data.Tests;

public class StoreStartTests
{
    [Fact]
    public void StartAppliesMigrationsAndOpensAsDefaultUser()
    {
        using var db = new TempDatabase();
        IAttenRepository store = AttenStore.Start(db.DbFile);

        store.Personnel.Add("علی", "رضایی", 8);
        PersonnelRecord person = Assert.Single(store.Personnel.List());
        Assert.Equal(DefaultUser.Id, person.CreatedBy);
        Assert.Equal(DefaultUser.Id, person.UpdatedBy);
        Assert.NotEmpty(DatabaseBackup.SnapshotPaths(db.DbFile));
        Assert.Empty(Migrator.Apply(db.DbFile));
    }

    [Fact]
    public void StartWithoutPendingMigrationsDoesNotAddASnapshot()
    {
        using var db = new TempDatabase();
        AttenStore.Start(db.DbFile);
        int count = DatabaseBackup.SnapshotPaths(db.DbFile).Count;
        Assert.True(count > 0);

        AttenStore.Start(db.DbFile);

        Assert.Equal(count, DatabaseBackup.SnapshotPaths(db.DbFile).Count);
        Assert.Empty(Migrator.Pending(db.DbFile));
    }
}
