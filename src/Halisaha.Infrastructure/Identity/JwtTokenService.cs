using System.Security.Claims;
using System.Text;
using Halisaha.Application.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Halisaha.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "halisaha-api";
    public string Audience { get; set; } = "halisaha-clients";

    /// <summary>At least 32 characters. Never in appsettings.json: user-secrets in development, an environment variable in production.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 60;
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(AuthenticatedUser user)
    {
        var o = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(o.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256),
        });
        return (token, expiresAt);
    }
}
