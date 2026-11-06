using Halisaha.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Halisaha.Infrastructure.Authentication;

/// <summary>
/// <see cref="ITokenService"/>'in JWT implementasyonu. Claim seti L2'deki gibi tam olarak
/// <c>sub, iat, exp, iss, aud</c>. Singleton: durum taşımaz, anahtar ve ayarlar açılışta bir kez okunur.
/// </summary>
internal sealed class JwtTokenService : ITokenService
{
    // SetDefaultTimesOnTokenCreation kapalı: açıkken kütüphane descriptor'da olmayan zaman claim'lerini kendisi
    // ekler (ör. nbf) ve değerini TimeProvider'dan değil gerçek saatten alır. Hem L2'deki claim setini hem de
    // "now dışarıdan verilir" kuralını (Karar 7) bozardı. Zamanlar aşağıda açıkça verilir.
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly SigningCredentials _signingCredentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        // .Value doğrulamayı tetikler; ayar bozuksa servis hiç oluşmaz.
        var jwt = options.Value;
        _signingCredentials = new SigningCredentials(JwtTokenValidation.CreateSigningKey(jwt), JwtTokenValidation.Algorithm);
        _issuer = jwt.Issuer!;
        _audience = jwt.Audience!;
        _lifetime = TimeSpan.FromMinutes(jwt.AccessTokenLifetimeMinutes);
        _timeProvider = timeProvider;
    }

    public AccessToken CreateAccessToken(Guid userId)
    {
        // Domain'deki kural: boş Guid kimlik değildir. Buraya gelmesi bizim hatamızdır (500), kullanıcı hatası değil.
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A token cannot be created for an empty user id.", nameof(userId));
        }

        var now = _timeProvider.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            IssuedAt = now.UtcDateTime,
            Expires = (now + _lifetime).UtcDateTime,
            Claims = new Dictionary<string, object> { [JwtTokenValidation.SubjectClaimType] = userId.ToString() },
            SigningCredentials = _signingCredentials,
        };

        return new AccessToken(_handler.CreateToken(descriptor), _lifetime);
    }
}
