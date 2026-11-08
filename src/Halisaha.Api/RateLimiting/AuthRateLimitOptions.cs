namespace Halisaha.Api.RateLimiting;

/// <summary>
/// Kayıt ve girişin IP başına limiti (Dilim 5). Değer appsettings.json'da; kod değişmeden ayarlanabilir.
/// Başlangıç değeri 20 / dakika bir tahmin: mobil operatörler CGNAT ile çok kullanıcıyı aynı IP'den çıkarır,
/// halısahada QR okutan bir takım aynı kovaya düşer. Gerçek trafik görülünce (429 metrikleri) yeniden ayarlanır.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>Bir dakikalık pencerede aynı kovadan kabul edilen istek sayısı (en az 1).</summary>
    public int PermitLimit { get; set; } = 20;
}
