using Halisaha.Infrastructure.Authentication;
using Halisaha.Infrastructure.Tests.TestData;

namespace Halisaha.Infrastructure.Tests.Authentication;

/// <summary>
/// L6 kuralları, veritabanı yok. Uygulamanın bu kurallarla açılmadığını Api testi (fail-fast) kanıtlar;
/// burada kuralların kendisi ve sınırları.
/// </summary>
public class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void ValidOptions_Succeed()
    {
        var result = _validator.Validate(name: null, TestJwt.Options());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingSigningKey_Fails_AndSaysWhereToSetIt(string? signingKey)
    {
        var options = TestJwt.Options();
        options.SigningKey = signingKey;

        var failure = Assert.Single(_validator.Validate(null, options).Failures!);

        Assert.Contains("Jwt:SigningKey is not configured", failure);
        Assert.Contains("user-secrets", failure);
        Assert.Contains("Jwt__SigningKey", failure);
    }

    [Fact]
    public void NonBase64SigningKey_Fails()
    {
        var options = TestJwt.Options("this is not base64!");

        var failure = Assert.Single(_validator.Validate(null, options).Failures!);

        Assert.Contains("not valid Base64", failure);
    }

    [Theory]
    [InlineData(31, false)]
    [InlineData(32, true)] // HS256'nın alt sınırı (256 bit)
    [InlineData(64, true)]
    public void SigningKeyLength_IsCheckedInDecodedBytes(int bytes, bool expectedValid)
    {
        var result = _validator.Validate(null, TestJwt.Options(TestJwt.NewSigningKey(bytes)));

        Assert.Equal(expectedValid, result.Succeeded);
    }

    [Fact]
    public void FailureMessages_NeverContainTheKey()
    {
        // 31 bayt: geçerli Base64 ama kısa. Mesajda uzunluk var, anahtarın kendisi yok.
        var signingKey = TestJwt.NewSigningKey(31);

        var failure = Assert.Single(_validator.Validate(null, TestJwt.Options(signingKey)).Failures!);

        Assert.Contains("at least 32 bytes", failure);
        Assert.DoesNotContain(signingKey, failure);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingIssuerOrAudience_Fails(string? value)
    {
        var options = TestJwt.Options();
        options.Issuer = value;
        options.Audience = value;

        var failures = _validator.Validate(null, options).Failures!.ToList();

        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, f => f.Contains("Jwt:Issuer"));
        Assert.Contains(failures, f => f.Contains("Jwt:Audience"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    [InlineData(-5, false)]
    public void Lifetime_MustBeBetweenOneAndSixtyMinutes(int minutes, bool expectedValid)
    {
        var options = TestJwt.Options();
        options.AccessTokenLifetimeMinutes = minutes;

        var result = _validator.Validate(null, options);

        Assert.Equal(expectedValid, result.Succeeded);
    }

    [Fact]
    public void AllFailures_AreReportedTogether()
    {
        // Ayarı düzelten kişi hataları tek tek keşfetmesin.
        var result = _validator.Validate(null, new JwtOptions());

        Assert.Equal(4, result.Failures!.Count());
    }
}
