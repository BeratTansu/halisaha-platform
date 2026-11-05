using Halisaha.Domain.Entities;
using Halisaha.Infrastructure.Persistence;
using Halisaha.Infrastructure.Persistence.Seed;
using Halisaha.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Halisaha.Infrastructure.Tests.Seed;

/// <summary>
/// Seed'in gerçek PostgreSQL'deki davranışı. Her test kendi boş veritabanını kullanır (global sayım ve DB'yi
/// değiştiren adımlar diğer testleri etkilemesin). Seed, uygulamadaki gibi Migrate ile tetiklenir.
/// </summary>
[Collection(LocationSeedCollection.Name)]
public class LocationSeedTests(LocationSeedFixture fixture)
{
    private static readonly IReadOnlyList<ProvinceSeed> Json = LocationSeeder.LoadSeedData();

    [Fact]
    public async Task MigrateAsync_LoadsDatabaseExactlyAsJson()
    {
        var options = await fixture.CreateEmptyDatabaseAsync();
        await MigrateAsync(options);

        await AssertDatabaseMatchesJsonAsync(options);
    }

    // İkinci çalıştırma senkron Migrate ile: EF araçları ve bundle'lar UseSeeding'i (sync) kullanır.
    [Fact]
    public async Task SecondRun_ThroughSyncMigrate_AddsNothing()
    {
        var options = await fixture.CreateEmptyDatabaseAsync();
        await MigrateAsync(options);

        using (var context = new HalisahaDbContext(options))
        {
            context.Database.Migrate(); // bekleyen migration yok, seed yine çalışır
        }

        await AssertDatabaseMatchesJsonAsync(options);
    }

    [Fact]
    public async Task Reseed_RestoresChangedNamesAndProvince()
    {
        var options = await fixture.CreateEmptyDatabaseAsync();
        await MigrateAsync(options);

        await using (var context = new HalisahaDbContext(options))
        {
            await context.Provinces.Where(p => p.Id == 34)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, "Bozuk İl"));
            await context.Districts.Where(d => d.Id == 32007)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Name, "Bozuk İlçe").SetProperty(d => d.ProvinceId, 1));
        }

        await MigrateAsync(options);

        await using (var context = new HalisahaDbContext(options))
        {
            Assert.Equal("İstanbul", (await context.Provinces.SingleAsync(p => p.Id == 34)).Name);
            var district = await context.Districts.SingleAsync(d => d.Id == 32007);
            Assert.Equal("Merkez", district.Name);
            Assert.Equal(32, district.ProvinceId);
        }
    }

    // Sahalar ve profiller bu kayıtlara bağlı olabilir; JSON'dan çıkmış olmaları onları silmez (uyarı loglanır).
    [Fact]
    public async Task Reseed_KeepsRowsThatAreNotInJson()
    {
        var options = await fixture.CreateEmptyDatabaseAsync();
        await MigrateAsync(options);

        await using (var context = new HalisahaDbContext(options))
        {
            context.Provinces.Add(new Province(999, "JSON'da Olmayan İl"));
            context.Districts.Add(new District(999001, 999, "JSON'da Olmayan İlçe"));
            await context.SaveChangesAsync();
        }

        await MigrateAsync(options);

        await using (var context = new HalisahaDbContext(options))
        {
            Assert.True(await context.Provinces.AnyAsync(p => p.Id == 999));
            Assert.True(await context.Districts.AnyAsync(d => d.Id == 999001));
        }
    }

    // EF seed'i migration geri alındıktan sonra da çağırır; tablolar yokken seed hata vermemeli.
    [Fact]
    public async Task DowngradeToZero_DoesNotFail_AndMigratingUpAgainReseeds()
    {
        var options = await fixture.CreateEmptyDatabaseAsync();
        await MigrateAsync(options);

        await using (var context = new HalisahaDbContext(options))
        {
            await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
            Assert.False(await TableExistsAsync(context, "Provinces"));
        }

        await MigrateAsync(options);
        await AssertDatabaseMatchesJsonAsync(options);
    }

    [Fact]
    public async Task Seeder_WithoutTables_DoesNothing()
    {
        var options = await fixture.CreateEmptyDatabaseAsync(); // migration uygulanmadı

        await using var context = new HalisahaDbContext(options);
        await LocationSeeder.SeedAsync(context, CancellationToken.None);
        LocationSeeder.Seed(context);

        Assert.False(await TableExistsAsync(context, "Provinces"));
    }

    private static async Task MigrateAsync(DbContextOptions<HalisahaDbContext> options)
    {
        await using var context = new HalisahaDbContext(options);
        await context.Database.MigrateAsync();
    }

    private static async Task AssertDatabaseMatchesJsonAsync(DbContextOptions<HalisahaDbContext> options)
    {
        await using var context = new HalisahaDbContext(options);

        var expectedProvinces = Json.Select(p => (p.Id, p.Name)).Order().ToList();
        var actualProvinces = (await context.Provinces.Select(p => new { p.Id, p.Name }).ToListAsync())
            .Select(p => (p.Id, p.Name)).Order().ToList();
        Assert.Equal(81, actualProvinces.Count);
        Assert.Equal(expectedProvinces, actualProvinces);

        var expectedDistricts = Json.SelectMany(p => p.Districts.Select(d => (d.Id, ProvinceId: p.Id, d.Name))).Order().ToList();
        var actualDistricts = (await context.Districts.Select(d => new { d.Id, d.ProvinceId, d.Name }).ToListAsync())
            .Select(d => (d.Id, d.ProvinceId, d.Name)).Order().ToList();
        Assert.Equal(973, actualDistricts.Count);
        Assert.Equal(expectedDistricts, actualDistricts);
    }

    private static Task<bool> TableExistsAsync(HalisahaDbContext context, string table) =>
        context.Database.SqlQuery<bool>($"SELECT to_regclass({"\"" + table + "\""}) IS NOT NULL AS \"Value\"").SingleAsync();
}
