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
}
