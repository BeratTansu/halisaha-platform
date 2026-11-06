using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Halisaha.Infrastructure.Authentication;

/// <summary>
/// "Token'ı nasıl üretiyorsak öyle doğrularız" bilgisinin tek yeri: algoritma, anahtar, issuer/audience, skew ve
/// claim adları. <see cref="JwtTokenService"/> üretirken, Api'deki JwtBearer doğrularken buradan okur.
/// Public olmasının tek sebebi Api'nin composition root'u (Program.cs): Api → Infrastructure bağımlılığı zaten
/// var ve yalnızca orada kullanılır. Bilinçli olarak JwtBearer'a bağımlı değildir; Infrastructure HTTP
/// kimlik doğrulama middleware'ini tanımaz, yalnızca IdentityModel'in parametrelerini üretir.
/// </summary>
public static class JwtTokenValidation
{
    /// <summary>L6: tek algoritma. Doğrulamada da yalnızca bu kabul edilir (algoritma karışıklığına karşı).</summary>
    internal const string Algorithm = SecurityAlgorithms.HmacSha256;

    /// <summary>L6: varsayılan 5 dk yerine 30 sn (15 dk'lık token'ın ömrünü üçte bir uzatmasın).</summary>
    internal static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>L2: kullanıcının Id'si. Api, MapInboundClaims kapalıyken claim'i bu adla okur.</summary>
    public const string SubjectClaimType = JwtRegisteredClaimNames.Sub;

    /// <summary>Admin diliminde eklenecek rol claim'inin adı; RequireRole bu adı okusun diye şimdiden ayarlı.</summary>
    public const string RoleClaimType = "role";

    /// <summary>
    /// Doğrulanmış <see cref="JwtOptions"/>'tan doğrulama parametreleri. Varsayılanı zaten açık olan kontroller
    /// (issuer, audience, lifetime, exp zorunluluğu, imza zorunluluğu) burada yine de açıkça yazılır: niyet
    /// görünür olsun ve bir kütüphane sürümü varsayılanı değiştirirse davranış sessizce değişmesin.
    /// </summary>
    public static TokenValidationParameters CreateParameters(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            IssuerSigningKey = CreateSigningKey(options),
            ValidAlgorithms = [Algorithm],
            ClockSkew = ClockSkew,
            NameClaimType = SubjectClaimType,
            RoleClaimType = RoleClaimType,
        };
    }

    internal static SymmetricSecurityKey CreateSigningKey(JwtOptions options) =>
        new(TryDecodeSigningKey(options.SigningKey)
            ?? throw new InvalidOperationException("Jwt:SigningKey is not valid Base64; options must be validated first."));

    /// <summary>Geçerli Base64 değilse null. Validator ve anahtar oluşturma aynı çözümü kullanır.</summary>
    internal static byte[]? TryDecodeSigningKey(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return null;
        }

        var buffer = new byte[signingKey.Length];
        return Convert.TryFromBase64String(signingKey, buffer, out var written) ? buffer[..written] : null;
    }
}
