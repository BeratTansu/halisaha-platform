using System.Security.Claims;
using Halisaha.Infrastructure.Authentication;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Halisaha.Infrastructure.Tests.Authentication;

/// <summary>
/// Üretim ve doğrulama aynı ayarlardan kurulunca birbirini kabul eder; ayarlardan biri farklıysa reddeder.
/// Doğrulama her zaman gerçek saatle yapılır (TokenValidationParameters'ta TimeProvider yok; 6 Kasım'da
/// kontrol edildi), bu yüzden süre testleri token'ı gerçek "şimdi"ye göre geriye dönük üretir.
/// </summary>
public class JwtTokenValidationTests
{
    private static readonly Guid UserId = Guid.Parse("0b9a6c2e-3f1d-4e8b-a7c5-2d9e4f6a1b38");
    private readonly JsonWebTokenHandler _handler = new();

    [Fact]
    public async Task TokenFromService_IsAccepted_AndSubIsTheName()
    {
        var options = TestJwt.Options();
        var token = Issue(options, DateTimeOffset.UtcNow);

        var result = await _handler.ValidateTokenAsync(token, JwtTokenValidation.CreateParameters(options));

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(UserId.ToString(), result.ClaimsIdentity.Name); // NameClaimType = sub
        Assert.Equal(UserId.ToString(), result.ClaimsIdentity.FindFirst(JwtTokenValidation.SubjectClaimType)?.Value);
    }

    [Fact]
    public async Task OtherSigningKey_IsRejected()
    {
        var token = Issue(TestJwt.Options(), DateTimeOffset.UtcNow);

        var result = await _handler.ValidateTokenAsync(token, JwtTokenValidation.CreateParameters(TestJwt.Options()));

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(result.Exception);
    }

    [Fact]
    public async Task OtherIssuer_IsRejected()
    {
        var signingKey = TestJwt.NewSigningKey();
        var issuing = TestJwt.Options(signingKey);
        issuing.Issuer = "someone-else";

        var result = await _handler.ValidateTokenAsync(
            Issue(issuing, DateTimeOffset.UtcNow), JwtTokenValidation.CreateParameters(TestJwt.Options(signingKey)));

        Assert.IsType<SecurityTokenInvalidIssuerException>(result.Exception);
    }

    [Fact]
    public async Task OtherAudience_IsRejected()
    {
        var signingKey = TestJwt.NewSigningKey();
        var issuing = TestJwt.Options(signingKey);
        issuing.Audience = "another-api";

        var result = await _handler.ValidateTokenAsync(
            Issue(issuing, DateTimeOffset.UtcNow), JwtTokenValidation.CreateParameters(TestJwt.Options(signingKey)));

        Assert.IsType<SecurityTokenInvalidAudienceException>(result.Exception);
    }

    [Theory]
    [InlineData(20, true)]  // süresi 20 sn önce doldu: 30 sn skew içinde
    [InlineData(40, false)] // 40 sn önce doldu: skew dışında (varsayılan 5 dk olsaydı kabul edilirdi)
    public async Task ExpiredToken_IsToleratedOnlyWithinThirtySeconds(int secondsSinceExpiry, bool expectedValid)
    {
        var options = TestJwt.Options();
        var issuedAt = DateTimeOffset.UtcNow
            - TimeSpan.FromMinutes(options.AccessTokenLifetimeMinutes)
            - TimeSpan.FromSeconds(secondsSinceExpiry);

        var result = await _handler.ValidateTokenAsync(Issue(options, issuedAt), JwtTokenValidation.CreateParameters(options));

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.IsType<SecurityTokenExpiredException>(result.Exception);
        }
    }

    [Fact]
    public async Task SameKeyButOtherAlgorithm_IsRejected()
    {
        // Anahtarı bilen biri bile HS256 dışında imzalayamaz (ValidAlgorithms). Kısıt olmasaydı HS512 geçerdi:
        // aynı simetrik anahtar ikisini de imzalar.
        var options = TestJwt.Options();
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = UserId.ToString() },
            SigningCredentials = new SigningCredentials(
                JwtTokenValidation.CreateSigningKey(options), SecurityAlgorithms.HmacSha512),
        });

        var parameters = JwtTokenValidation.CreateParameters(options);
        var result = await _handler.ValidateTokenAsync(token, parameters);

        // IdentityModel 8.19.2 bunu SecurityTokenSignatureKeyNotFoundException olarak raporluyor (algoritmaya
        // uyan anahtar bulunamadı); tipe değil sonuca bakılır. Kontrol adımı: reddin sebebi gerçekten kısıt mı?
        Assert.False(result.IsValid);
        parameters.ValidAlgorithms = null;
        var withoutRestriction = await _handler.ValidateTokenAsync(token, parameters);
        Assert.True(withoutRestriction.IsValid, "Without ValidAlgorithms the HS512 token should be accepted; otherwise this test proves nothing.");
    }

    [Fact]
    public async Task UnsignedToken_IsRejected()
    {
        var options = TestJwt.Options();
        var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var token = $"{Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""")}." +
                    $"{Base64UrlEncoder.Encode($$"""{"sub":"{{UserId}}","iss":"{{options.Issuer}}","aud":"{{options.Audience}}","exp":{{exp}}}""")}.";

        var result = await _handler.ValidateTokenAsync(token, JwtTokenValidation.CreateParameters(options));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parameters_StateTheDecisionsExplicitly()
    {
        var parameters = JwtTokenValidation.CreateParameters(TestJwt.Options());

        Assert.Equal([SecurityAlgorithms.HmacSha256], parameters.ValidAlgorithms);
        Assert.Equal(TimeSpan.FromSeconds(30), parameters.ClockSkew);
        Assert.Equal("sub", parameters.NameClaimType);
        Assert.Equal("role", parameters.RoleClaimType);
        Assert.True(parameters.ValidateIssuer && parameters.ValidateAudience && parameters.ValidateLifetime);
        Assert.True(parameters.RequireExpirationTime && parameters.RequireSignedTokens);
        Assert.NotEqual(ClaimTypes.NameIdentifier, parameters.NameClaimType);
    }

    private static string Issue(JwtOptions options, DateTimeOffset issuedAt) =>
        JwtTokenServiceTests.CreateService(issuedAt, options).CreateAccessToken(UserId).Value;
}
