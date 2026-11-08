using Halisaha.Infrastructure.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Halisaha.Api.Tests.Fixtures;

/// <summary>
/// Uygulamanın kendi servisinin üretemeyeceği token'lar (süresi dolmuş, sub'sız, sub'ı Guid olmayan). Host'un
/// gerçek anahtarı, issuer'ı ve audience'ıyla imzalanır: reddedilirlerse sebep yalnızca değiştirilen alandır.
/// </summary>
public static class TestTokens
{
    public static string Create(
        WebApplicationFactory<Program> factory,
        IDictionary<string, object> claims,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? expires = null)
    {
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var now = DateTimeOffset.UtcNow;
        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };

        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = (issuedAt ?? now).UtcDateTime,
            Expires = (expires ?? now.AddMinutes(5)).UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(options.SigningKey!)), SecurityAlgorithms.HmacSha256),
        });
    }
}
