using Halisaha.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Halisaha.Api.Tests.Fixtures;

/// <summary>
/// Gerçek uygulama (Program.cs'in bütün kayıtları ve pipeline'ı) + gerçek PostgreSQL 17. Ortam "Testing":
/// appsettings.Development.json (yerel 5434 bağlantısı) yüklenmez; connection string yalnızca buradan gelir.
/// Uygulama başlarken migration çalıştırmaz (bilinçli karar); şemayı ve il/ilçe seed'ini fixture kurar.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Seed'den gelen gerçek bir ilçe. Testler kendi ilçesini oluşturmaz; API'nin gördüğü veri budur.</summary>
    public int SeededDistrictId { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Factory = CreateFactory();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HalisahaDbContext>();
        await context.Database.MigrateAsync(); // UseHalisahaSeeding: il/ilçe verisi de yüklenir
        SeededDistrictId = await context.Districts.OrderBy(d => d.Id).Select(d => d.Id).FirstAsync();
    }

    /// <summary>Uygulamanın bir parçasını değiştiren testler için (ör. 500 üretmek): aynı veritabanı, ayrı host.</summary>
    public WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            // UseSetting, Program.cs'in builder.Build()'den önceki okumasına (fail-fast kontrolü) ulaşır.
            builder.UseSetting("ConnectionStrings:Halisaha", _container.GetConnectionString());
            configure?.Invoke(builder);
        });

    public async Task<T> QueryAsync<T>(Func<HalisahaDbContext, Task<T>> query)
    {
        using var scope = Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<HalisahaDbContext>());
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
