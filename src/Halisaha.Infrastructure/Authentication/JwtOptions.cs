namespace Halisaha.Infrastructure.Authentication;

/// <summary>
/// JWT ayarlarının tek kaynağı (Karar 11, L6). Token üretimi (<see cref="JwtTokenService"/>) ve doğrulaması
/// (Api'deki JwtBearer, <see cref="JwtTokenValidation"/> üzerinden) aynı nesneden okur; iki taraf ayrı ayardan
/// okusaydı ör. issuer'daki bir harf farkı API'nin kendi ürettiği token'ı reddetmesine yol açardı.
/// Kurallar <see cref="JwtOptionsValidator"/>'da; uygulama açılırken doğrulanır (ValidateOnStart).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 için kütüphanenin alt sınırı: 256 bit (6 Kasım'da ölçüldü; altı imzalarken IDX10720).</summary>
    public const int MinSigningKeyBytes = 32;

    public const int MinAccessTokenLifetimeMinutes = 1;

    /// <summary>Canlıdaki bir yazım hatası günlerce yaşayan token üretmesin.</summary>
    public const int MaxAccessTokenLifetimeMinutes = 60;

    /// <summary>
    /// Base64 kodlanmış rastgele bayt. Repoya hiçbir ortamın anahtarı girmez: geliştirmede user-secrets,
    /// diğer ortamlarda <c>Jwt__SigningKey</c> ortam değişkeni.
    /// </summary>
    public string? SigningKey { get; set; }

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    /// <summary>
    /// Dakika (int, TimeSpan değil: config'deki "15" TimeSpan olarak 15 gün okunur). Refresh token dilimi
    /// tamamlanana kadar geçici olarak 60, sonra 15.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; }
}
