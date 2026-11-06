using System.Security.Claims;
using Halisaha.Api.Tests.Fixtures;
using Halisaha.Application.Abstractions.Identity;
using Halisaha.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Halisaha.Api.Tests.Auth;

/// <summary>
/// Uygulamanın kendi ürettiği token'ı uygulamanın kendi JwtBearer'ı kabul eder: iki taraf aynı JwtOptions'tan
/// kuruludur. Token, gerçek JwtBearer handler'ından geçirilir (MapInboundClaims dahil); handler'ı
/// <see cref="AuthenticateProbe"/> çağırır. Veritabanı gerekmez. Doğrulama kurallarının ayrıntısı (skew, algoritma,
/// issuer/audience) Infrastructure testlerinde; login'in ürettiği token için LoginEndpointTests.
/// </summary>
public sealed class JwtBearerConfigurationTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("4e2b8d1a-7c3f-4a9e-b5d6-0f1e2a3b4c5d");

    private readonly WebApplicationFactory<Program> _factory =
        TestApp.Create(TestApp.UnusedConnectionString, AuthenticateProbe.AddTo);

    [Fact]
    public async Task TokenFromTheApp_IsAuthenticated_WithClaimsUnderTheirTokenNames()
    {
        var token = _factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(UserId);

        var probe = await AuthenticateAsync(token.Value);

        Assert.True(probe.Succeeded, probe.Failure);
        Assert.Equal(UserId.ToString(), probe.Name);
        // MapInboundClaims kapalı: "sub" adıyla gelir, SOAP URI'sine (NameIdentifier) dönüşmez.
        Assert.Contains("sub", probe.ClaimTypes);
        Assert.DoesNotContain(ClaimTypes.NameIdentifier, probe.ClaimTypes);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsNotAuthenticated()
    {
        // Başka bir host kendi rastgele anahtarıyla üretti: doğru biçim, yanlış imza.
        using var otherApp = TestApp.Create(TestApp.UnusedConnectionString);
        var foreignToken = otherApp.Services.GetRequiredService<ITokenService>().CreateAccessToken(UserId);

        var probe = await AuthenticateAsync(foreignToken.Value);

        Assert.False(probe.Succeeded);
    }

    [Fact]
    public async Task NoToken_IsNoResult_NotAFailure()
    {
        // Anonim endpoint'ler (register, login) token'sız isteklerde etkilenmez.
        var probe = await AuthenticateAsync(token: null);

        Assert.False(probe.Succeeded);
        Assert.True(probe.None);
    }

    [Fact]
    public void BearerOptions_ComeFromTheSharedJwtOptions()
    {
        var jwt = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var bearer = _factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.False(bearer.MapInboundClaims);
        Assert.Equal(jwt.Issuer, bearer.TokenValidationParameters.ValidIssuer);
        Assert.Equal(jwt.Audience, bearer.TokenValidationParameters.ValidAudience);
        Assert.Equal(TimeSpan.FromSeconds(30), bearer.TokenValidationParameters.ClockSkew);
        Assert.Equal(["HS256"], bearer.TokenValidationParameters.ValidAlgorithms);
    }

    private async Task<AuthenticateProbe.Result> AuthenticateAsync(string? token)
    {
        using var client = _factory.CreateClient();
        return await AuthenticateProbe.AuthenticateAsync(client, token);
    }

    public void Dispose() => _factory.Dispose();
}
