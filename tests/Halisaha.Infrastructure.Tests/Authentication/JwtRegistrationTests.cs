using Halisaha.Application.Abstractions.Identity;
using Halisaha.Infrastructure.Authentication;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Halisaha.Infrastructure.Tests.Authentication;

/// <summary>
/// AddInfrastructure'ın JWT kayıtları, veritabanı yok (DbContext kaydı bağlantı açmaz). Uygulamanın kendi
/// DI kurulumu kullanılır; elle kurulmuş bir servis başka bir şeyi test ederdi.
/// </summary>
public class JwtRegistrationTests
{
    private const string UnusedConnectionString = "Host=unused.invalid;Database=unused";

    [Fact]
    public void TokenService_IsRegisteredAsSingleton_WithTheGivenOptions()
    {
        using var provider = Build(TestJwt.Configure());

        var first = provider.GetRequiredService<ITokenService>();
        var second = provider.GetRequiredService<ITokenService>();

        Assert.IsType<JwtTokenService>(first);
        Assert.Same(first, second);
        Assert.Equal(TestJwt.Issuer, provider.GetRequiredService<IOptions<JwtOptions>>().Value.Issuer);
    }

    [Fact]
    public void InvalidOptions_AreRejectedByTheRegisteredValidator()
    {
        using var provider = Build(o => o.Issuer = TestJwt.Issuer); // anahtar, audience, süre eksik

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<ITokenService>());

        Assert.Contains(exception.Failures, f => f.Contains("Jwt:SigningKey"));
    }

    private static ServiceProvider Build(Action<JwtOptions> configureJwt)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(UnusedConnectionString, configureJwt);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
