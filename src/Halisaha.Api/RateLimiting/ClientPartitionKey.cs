using System.Net;

namespace Halisaha.Api.RateLimiting;

/// <summary>
/// Rate limit'te "bu istek hangi kovaya düşer?" sorusunun cevabı. Aynı anahtarı üreten istekler aynı kotayı paylaşır.
/// </summary>
internal static class ClientPartitionKey
{
    /// <summary>IP adresi bilinmeyen isteklerin ortak kovası: adres yok diye limit devre dışı kalmaz.</summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// IPv4: adresin kendisi ("203.0.113.5"). IPv4-mapped IPv6 ("::ffff:203.0.113.5") de IPv4 sayılır.
    /// IPv6: adresin ilk 64 biti, ağ adresi + "/64" biçiminde ("2001:db8:1:2::/64"). Adres yoksa <see cref="Unknown"/>.
    /// </summary>
    public static string From(IPAddress? address)
    {
        // TODO(Berat): Dilim 5. Şimdilik her istek aynı kovaya düşüyor; ClientPartitionKeyTests kırmızı.
        return Unknown;
    }
}
