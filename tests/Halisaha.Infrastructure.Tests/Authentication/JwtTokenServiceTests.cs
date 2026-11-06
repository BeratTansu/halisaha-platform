using System.Text.Json;
using Halisaha.Infrastructure.Authentication;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Halisaha.Infrastructure.Tests.Authentication;

/// <summary>
/// Token'ın içeriği: claim seti, header ve zamanlar. Token, kütüphanenin kendi okuyucusuyla değil ham JSON
/// olarak açılır; böylece kütüphanenin kendiliğinden eklediği her alan (ör. nbf, jti, kid) görünür.
/// </summary>
public class JwtTokenServiceTests
{
    private static readonly Guid UserId = Guid.Parse("6f1c2a5e-8d3b-4c7a-9e2f-1b4d6a8c0e57");

    [Fact]
    public void Payload_ContainsExactlyTheClaimsDecidedInL2()
    {
        var token = CreateService(TestClock.Now).CreateAccessToken(UserId);

        var payload = DecodeSegment(token.Value, segment: 1);

        // L2: sub, iat, exp + iss, aud. Kütüphane nbf'yi ancak SetDefaultTimesOnTokenCreation açıkken ekler;
        // jti ve diğerleri hiç eklenmez. Bu test o ayarın kapalı kaldığının da kanıtı.
        Assert.Equal(["aud", "exp", "iat", "iss", "sub"], payload.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(UserId.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal(TestJwt.Issuer, payload.GetProperty("iss").GetString());
        Assert.Equal(TestJwt.Audience, payload.GetProperty("aud").GetString());
    }

    [Fact]
    public void Header_ContainsOnlyHs256AndType()
    {
        var token = CreateService(TestClock.Now).CreateAccessToken(UserId);

        var header = DecodeSegment(token.Value, segment: 0);

        // kid yok: tek anahtar (L6). Anahtar rotasyonu v2'de gelirse kid burada görünür.
        Assert.Equal(["alg", "typ"], header.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(SecurityAlgorithms.HmacSha256, header.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.GetProperty("typ").GetString());
    }

    [Fact]
    public void Times_ComeFromTimeProvider_AndLifetimeFromOptions()
    {
        var token = CreateService(TestClock.Now).CreateAccessToken(UserId);

        var payload = DecodeSegment(token.Value, segment: 1);

        // Sabit test saati (2026-11-01) gerçek saatten günlerce farklı: değer gerçek saatten gelseydi tutmazdı.
        Assert.Equal(TestClock.Now.ToUnixTimeSeconds(), payload.GetProperty("iat").GetInt64());
        Assert.Equal(TestClock.Now.AddMinutes(TestJwt.LifetimeMinutes).ToUnixTimeSeconds(), payload.GetProperty("exp").GetInt64());
        Assert.Equal(TimeSpan.FromMinutes(TestJwt.LifetimeMinutes), token.ExpiresIn);
    }

    [Fact]
    public void ToString_DoesNotRevealTheToken()
    {
        var token = CreateService(TestClock.Now).CreateAccessToken(UserId);

        var text = token.ToString();

        Assert.DoesNotContain(token.Value, text);
        Assert.Contains("***", text);
    }

    [Fact]
    public void EmptyUserId_IsRejected()
    {
        var service = CreateService(TestClock.Now);

        Assert.Throws<ArgumentException>(() => service.CreateAccessToken(Guid.Empty));
    }

    [Fact]
    public void InvalidOptions_PreventTheServiceFromBeingCreated()
    {
        // Host dışında da (ValidateOnStart yokken) bozuk ayarla token üretilemez: .Value validator'ı çalıştırır.
        var options = new OptionsManager<JwtOptions>(new OptionsFactory<JwtOptions>(
            [new ConfigureOptions<JwtOptions>(o => o.Issuer = TestJwt.Issuer)],
            [],
            [new JwtOptionsValidator()]));

        Assert.Throws<OptionsValidationException>(() => new JwtTokenService(options, new FixedTimeProvider(TestClock.Now)));
    }

    internal static JwtTokenService CreateService(DateTimeOffset now, JwtOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? TestJwt.Options()), new FixedTimeProvider(now));

    private static JsonElement DecodeSegment(string token, int segment)
    {
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
        return JsonDocument.Parse(Base64UrlEncoder.Decode(parts[segment])).RootElement.Clone();
    }
}
