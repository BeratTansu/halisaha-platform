using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Application.Commands.Register;
using Halisaha.Application.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersRegisterHandlerWithItsValidator()
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
        services.AddSingleton(TimeProvider.System);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RegisterHandler>());
    }
}
