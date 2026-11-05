using Halisaha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Halisaha.Infrastructure.Tests.Fixtures;

/// <summary>
/// Seed testleri için ayrı container. Seed testleri global sayım yapar ve DB'yi değiştirir (ad bozma, fazla kayıt,
/// geri alma); her test bu yüzden kendi boş veritabanını alır ve test sırası sonucu etkilemez.
/// </summary>
public sealed class LocationSeedFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _container.StartAsync();

    /// <summary>Migration uygulanmamış, boş yeni bir veritabanı; uygulamayla aynı seeding ayarıyla.</summary>
    public async Task<DbContextOptions<HalisahaDbContext>> CreateEmptyDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString());
        var databaseName = $"seed_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        builder.Database = databaseName;
        return new DbContextOptionsBuilder<HalisahaDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .UseHalisahaSeeding()
            .Options;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
