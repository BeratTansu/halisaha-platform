using System.Net;
using Halisaha.Api.RateLimiting;

namespace Halisaha.Api.Tests.RateLimiting;

/// <summary>
/// "Bu istek hangi kovaya düşer?" Saf fonksiyon: HTTP ve veritabanı yok. Uçtan uca davranış AuthRateLimitingTests'te.
/// </summary>
public sealed class ClientPartitionKeyTests
{
    [Fact]
    public void NoAddress_FallsIntoTheSharedUnknownBucket()
    {
        // Adres yok diye limit devre dışı kalmaz: bütün adressiz istekler tek kovayı paylaşır.
        Assert.Equal(ClientPartitionKey.Unknown, ClientPartitionKey.From(null));
    }

    [Theory]
    [InlineData("203.0.113.5", "203.0.113.5")]
    [InlineData("198.51.100.7", "198.51.100.7")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    public void IPv4_IsItsOwnBucket(string address, string expectedKey)
    {
        Assert.Equal(expectedKey, ClientPartitionKey.From(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("::ffff:203.0.113.5", "203.0.113.5")]
    [InlineData("::ffff:127.0.0.1", "127.0.0.1")]
    public void IPv4MappedToIPv6_IsTheSameBucketAsThePlainIPv4(string address, string expectedKey)
    {
        // Kestrel çift yığınlı (dual-mode) soketlerde IPv4 istemciyi bu kılıkta verebilir. Yakalanmazsa aynı kişi
        // iki farklı kovaya düşer ve limitin iki katını kullanır.
        Assert.Equal(expectedKey, ClientPartitionKey.From(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("2001:db8:1:2::1")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd")]
    [InlineData("2001:0db8:0001:0002:ffff:ffff:ffff:ffff")] // aynı ağ, uzun yazım: metin değil bayt önemli
    public void IPv6_TheFirst64BitsAreTheBucket(string address)
    {
        // Bir ev ya da telefon genelde bütün bir /64 bloğuna sahiptir ve her istekte yeni adres seçebilir.
        Assert.Equal("2001:db8:1:2::/64", ClientPartitionKey.From(IPAddress.Parse(address)));
    }

    [Fact]
    public void IPv6_DifferentSlash64Networks_AreDifferentBuckets()
    {
        var first = ClientPartitionKey.From(IPAddress.Parse("2001:db8:1:2::1"));
        var second = ClientPartitionKey.From(IPAddress.Parse("2001:db8:1:3::1"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void IPv6Loopback_IsTheZeroNetwork()
    {
        Assert.Equal("::/64", ClientPartitionKey.From(IPAddress.IPv6Loopback));
    }
}
