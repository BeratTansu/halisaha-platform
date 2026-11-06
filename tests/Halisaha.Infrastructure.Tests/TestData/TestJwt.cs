using System.Security.Cryptography;
using Halisaha.Infrastructure.Authentication;

namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>
/// Testlerin JWT ayarları. Anahtar her çalışmada rastgele üretilir; repoda bilinen bir anahtar durmaz (L6).
/// Issuer/audience uygulamanın appsettings.json'daki değerleriyle aynı.
/// </summary>
public static class TestJwt
{
    public const string Issuer = "halisaha-api";
    public const string Audience = "halisaha-api";
    public const int LifetimeMinutes = 60;

    public static string NewSigningKey(int bytes = 64) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    public static JwtOptions Options(string? signingKey = null) => new()
    {
        SigningKey = signingKey ?? NewSigningKey(),
        Issuer = Issuer,
        Audience = Audience,
        AccessTokenLifetimeMinutes = LifetimeMinutes,
    };

    /// <summary><c>AddInfrastructure</c>'a verilen ayar adımı: <see cref="Options"/> ile aynı değerler.</summary>
    public static Action<JwtOptions> Configure(string? signingKey = null) => target =>
    {
        var source = Options(signingKey);
        target.SigningKey = source.SigningKey;
        target.Issuer = source.Issuer;
        target.Audience = source.Audience;
        target.AccessTokenLifetimeMinutes = source.AccessTokenLifetimeMinutes;
    };
}
