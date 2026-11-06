using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Application.Commands.Login;
using Halisaha.Application.Commands.Register;
using Halisaha.Application.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersRegisterHandlerWithItsValidator()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RegisterHandler>());
    }

    [Fact]
    public void AddApplication_RegistersLoginHandlerWithItsValidator()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LoginHandler>());
    }

    private static ServiceProvider BuildProvider()
    {
        // Port'lar Infrastructure'ın, TimeProvider composition root'un işi; burada sahteleriyle doldurulur.
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<CallLog>();
        services.AddScoped<FakeUnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<FakeUnitOfWork>());
        services.AddScoped<IIdentityService, FakeIdentityService>();
        services.AddScoped<IPlayerProfileRepository, FakePlayerProfileRepository>();
        services.AddScoped<ILocationQueries, FakeLocationQueries>();
        services.AddSingleton<ITokenService, FakeTokenService>();
        services.AddSingleton(TimeProvider.System);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
