using Halisaha.Infrastructure.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>
/// Uygulamanın kendi DI kurulumu (<see cref="DependencyInjection.AddInfrastructure"/>) gerçek PostgreSQL'e bağlı.
/// Register'ın transaction iddiası ("UserManager ile UnitOfWork aynı scoped context'i kullanır") ancak üretimdeki
/// kayıtlarla kanıtlanabilir; elle kurulmuş bir UserManager başka bir şeyi test ederdi.
/// </summary>
public static class InfrastructureServices
{
    public static ServiceProvider Build(PostgresFixture fixture, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        // Host'un (WebApplication) normalde verdiği altyapı: UserManager ve UnitOfWork logger ister,
        // Identity metrik toplayıcısı IMeterFactory ister. TimeProvider composition root'un işi (Program.cs);
        // token servisi ona bağımlı. Testler configure ile sahte saat verebilir (son kayıt kazanır).
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(fixture.ConnectionString, TestJwt.Configure());
        configure?.Invoke(services);

        // ValidateScopes: singleton'ın scoped bir servisi yakalaması (ör. context'i) hata verir.
        // ValidateOnBuild: eksik kayıt test başında patlar, ilk kullanımda değil.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
