using Halisaha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Halisaha.Infrastructure.Tests.Fixtures;

/// <summary>
/// Test çalıştırması boyunca tek bir PostgreSQL 17 container'ı. Başlarken gerçek migration'ları uygular;
/// testlere yalnızca yeni context ve çakışmasız il/ilçe Id'si verir. Geliştirme DB'sine (compose, 5434) dokunmaz:
/// Testcontainers rastgele bir host portu açar.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Compose ile aynı ana sürüm: testler geliştirme ortamıyla aynı motoru kullanır.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private DbContextOptions<HalisahaDbContext> _options = null!;
    // Seed gerçek il (1-81) ve ilçe (1001-81xxx) Id'lerini yükler; test verisi onlarla çakışmasın diye
    // sayaç çok yüksek bir değerden başlar. Testler seed'lenmiş verilere bağımlı değildir.
    private int _lastLocationId = 1_000_000;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<HalisahaDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseHalisahaSeeding() // uygulamayla aynı: Migrate il/ilçe verisini de yükler
            .Options;

        // EnsureCreated değil Migrate: test edilen şeyin bir parçası migration'ın kendisi.
        // EF 9+ model ile snapshot farklıysa burada exception fırlatır ("migration unutuldu" hatası yakalanır).
        // Dışarıdan transaction'a sarılmaz; Migrate kendi transaction'ını ve kilidini yönetir.
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Her çağrıda yeni bir context. "Yeni context'te yükle" adımları buna dayanır: aynı context
    /// bellekteki nesneyi döndürür ve DB'yi değil kendi önbelleğini test etmiş oluruz.
    /// </summary>
    public HalisahaDbContext CreateContext() => new(_options);

    /// <summary>
    /// Uygulamanın DI kurulumunu (AddInfrastructure) gerçek container'a bağlamak için. Context'i kendisi oluşturan
    /// testler <see cref="CreateContext"/>'i kullanır; scope içinde UserManager/UnitOfWork isteyen testler bunu.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Province/District Id'leri elle verilir (ValueGeneratedNever) ve adları unique'tir; testler paylaşılan
    /// DB'de çakışmasın diye her il ve ilçe buradan benzersiz bir Id alır.
    /// </summary>
    public int NextLocationId() => Interlocked.Increment(ref _lastLocationId);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
