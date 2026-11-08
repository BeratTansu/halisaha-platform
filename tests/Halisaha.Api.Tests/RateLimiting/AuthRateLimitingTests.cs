using System.Net;
using System.Text.Json;
using Halisaha.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace Halisaha.Api.Tests.RateLimiting;

/// <summary>
/// Kayıt ve girişin IP başına limiti, uçtan uca (gerçek pipeline). Limit 3'e düşürülür; istemci IP'si test başlığıyla
/// verilir (TestClientIp). Veritabanı gerekmez: istekler bilerek geçersiz (400). Limit sonuca bakmaz, her deneme
/// kotadan düşer. Pencerenin yenilenmesi test edilmez: o Microsoft'un limiter'ının davranışı, bizim kodumuz değil.
/// </summary>
public sealed class AuthRateLimitingTests
{
    private const int TestLimit = 3;
    private const string RegisterUrl = "/api/auth/register";
    private const string LoginUrl = "/api/auth/login";
    private const string ClientA = "203.0.113.10";
    private const string ClientB = "198.51.100.7";

    private static readonly object InvalidBody = new { };

    [Fact]
    public async Task SameClient_IsRejectedAfterTheLimit_WithProblemAndRetryAfter()
    {
        using var factory = Create();
        using var client = factory.CreateClient();

        for (var i = 0; i < TestLimit; i++)
        {
            var allowed = await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, ClientA));
            Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode); // limit içinde: istek controller'a ulaştı
        }

        var rejected = await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, ClientA));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("system.rate_limited", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));

        // Retry-After saniye cinsinden tam sayı, pencereden (60 sn) uzun olamaz.
        var retryAfter = Assert.Single(rejected.Headers.GetValues("Retry-After"));
        var seconds = int.Parse(retryAfter, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(seconds, 1, 60);
    }

    [Fact]
    public async Task RegisterAndLogin_ShareOneQuota()
    {
        using var factory = Create();
        using var client = factory.CreateClient();

        // Limit 3: register, login, register kotayı bitirir; sıradaki login reddedilir. Ayrı kotalar olsaydı
        // login'in kotasında hâlâ 2 hak kalırdı.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(TestClientIp.Post(RegisterUrl, InvalidBody, ClientA))).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, ClientA))).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(TestClientIp.Post(RegisterUrl, InvalidBody, ClientA))).StatusCode);

        var rejected = await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, ClientA));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task DifferentClients_HaveSeparateQuotas()
    {
        using var factory = Create();
        using var client = factory.CreateClient();
        await ExhaustAsync(client, ClientA);

        var otherClient = await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, ClientB));

        // A'nın kotası doldu; B bundan etkilenmez.
        Assert.Equal(HttpStatusCode.BadRequest, otherClient.StatusCode);
    }

    [Fact]
    public async Task SameIPv6Network_SharesTheQuota()
    {
        using var factory = Create();
        using var client = factory.CreateClient();
        await ExhaustAsync(client, "2001:db8:1:2::1");

        var sameNetwork = await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, "2001:db8:1:2:aaaa::9"));
        var otherNetwork = await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, "2001:db8:1:3::1"));

        Assert.Equal(HttpStatusCode.TooManyRequests, sameNetwork.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, otherNetwork.StatusCode);
    }

    [Fact]
    public async Task OtherEndpoints_AreNotLimited()
    {
        // Politika yalnızca register ve login'de. /api/me token'sız 401 döner; kaç kez sorulursa sorulsun 429 olmaz.
        using var factory = Create();
        using var client = factory.CreateClient();
        await ExhaustAsync(client, ClientA);

        for (var i = 0; i < TestLimit + 2; i++)
        {
            var response = await client.SendAsync(TestClientIp.Get("/api/me", ClientA));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("0", "PermitLimit must be at least 1")]
    [InlineData("-5", "PermitLimit must be at least 1")]
    [InlineData("abc", "RateLimiting:Auth:PermitLimit")]
    public void InvalidLimit_AppDoesNotStart(string value, string expectedMessage)
    {
        using var factory = TestApp.Create(TestApp.UnusedConnectionString, builder =>
            builder.UseSetting("RateLimiting:Auth:PermitLimit", value));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(Messages(exception), m => m.Contains(expectedMessage));
    }

    private static async Task ExhaustAsync(HttpClient client, string clientIp)
    {
        for (var i = 0; i < TestLimit; i++)
        {
            await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, clientIp));
        }

        var rejected = await client.SendAsync(TestClientIp.Post(LoginUrl, InvalidBody, clientIp));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode); // ön koşul: kota gerçekten doldu
    }

    private static WebApplicationFactory<Program> Create() =>
        TestApp.Create(TestApp.UnusedConnectionString, builder =>
        {
            builder.UseSetting("RateLimiting:Auth:PermitLimit", TestLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            TestClientIp.AddTo(builder);
        });

    private static IEnumerable<string> Messages(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current is OptionsValidationException validation ? string.Join(" ", validation.Failures) : current.Message;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Messages))
                {
                    yield return inner;
                }
            }
        }
    }
}
