using Halisaha.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class MigrationTests(PostgresFixture fixture)
{
    // Fixture zaten Migrate eder; başarısız olsaydı hiçbir test çalışmazdı. Bu test, "migration gerçek
    // PostgreSQL 17'de uygulandı" bilgisini test listesinde görünür kılar ve hepsinin uygulandığını doğrular.
    [Fact]
    public async Task Migrate_OnEmptyDatabase_AppliesEveryMigration()
    {
        await using var context = fixture.CreateContext();

        var all = context.Database.GetMigrations();
        var applied = await context.Database.GetAppliedMigrationsAsync();
        var pending = await context.Database.GetPendingMigrationsAsync();

        Assert.NotEmpty(all);
        Assert.Equal(all, applied);
        Assert.Empty(pending);
    }
}
