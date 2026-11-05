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
        // Identity metrik toplayıcısı IMeterFactory ister.
        services.AddLogging();
        services.AddMetrics();
        services.AddInfrastructure(fixture.ConnectionString);
        configure?.Invoke(services);

        // ValidateScopes: singleton'ın scoped bir servisi yakalaması (ör. context'i) hata verir.
        // ValidateOnBuild: eksik kayıt test başında patlar, ilk kullanımda değil.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
