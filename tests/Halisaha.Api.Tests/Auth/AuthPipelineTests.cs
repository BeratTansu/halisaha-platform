using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Halisaha.Api.Tests.Fixtures;
using Halisaha.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Halisaha.Api.Tests.Auth;

/// <summary>
/// Token'la korunan endpoint'lerin sözleşmesi (Karar 11, L3 + Dilim 3): 401/403 gövdeleri, RFC 6750
/// WWW-Authenticate başlığı, sub kontrolü ve fallback policy. Veritabanı gerekmez; endpoint'ler yalnızca test
/// projesinde duran <see cref="AuthTestController"/>'dır.
/// </summary>
public sealed class AuthPipelineTests : IDisposable
{
    private const string DefaultUrl = $"/{AuthTestController.BasePath}/default";
    private const string AdminUrl = $"/{AuthTestController.BasePath}/admin";
    private const string AnonymousActorUrl = $"/{AuthTestController.BasePath}/anonymous-actor";

    // Başlık değerleri bilerek string: istemcinin gördüğü sözleşme (RFC 6750 3 ve 3.1).
    private const string PlainChallenge = "Bearer";
    private const string InvalidTokenChallenge = "Bearer error=\"invalid_token\"";

    private static readonly Guid UserId = Guid.Parse("5c1e9a3b-7d2f-4e8a-b6c4-0a9d8e7f6b51");

    private readonly WebApplicationFactory<Program> _factory =
        TestApp.Create(TestApp.UnusedConnectionString, AuthTestController.AddTo);

    // ---- token yok ----

    [Fact]
    public async Task NoToken_Returns401Problem_WithPlainBearerChallenge()
    {
        var response = await GetAsync(DefaultUrl, authorization: null);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated");
        // RFC 6750 3.1: istekte kimlik bilgisi yoksa hata kodu eklenmemeli.
        Assert.Equal(PlainChallenge, WwwAuthenticate(response));
    }

    [Fact]
    public async Task OtherAuthorizationScheme_CountsAsNoToken()
    {
        // Bearer olmayan bir şema JwtBearer'a token olarak hiç ulaşmaz: "token gönderilmedi" durumu.
        var response = await GetAsync(DefaultUrl, new AuthenticationHeaderValue("Basic", "YWhtZXQ6cGFyb2xh"));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated");
        Assert.Equal(PlainChallenge, WwwAuthenticate(response));
    }

    // ---- token gönderildi ama reddedildi ----

    public static TheoryData<string> RejectedTokens => new()
    {
        "malformed",
        "other-key",
        "expired",
        "no-sub",
        "non-guid-sub",
        "empty-guid-sub",
    };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task RejectedToken_Returns401Problem_WithInvalidTokenChallenge(string kind)
    {
        var response = await GetAsync(DefaultUrl, Bearer(RejectedToken(kind)));

        using var json = await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated");
        Assert.Equal(InvalidTokenChallenge, WwwAuthenticate(response));
        // Sebep hiçbir yerde söylenmez: error_description yok, gövdede ayrıntı yok.
        Assert.DoesNotContain("error_description", WwwAuthenticate(response));
        Assert.Equal("Authentication is required.", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task NoTokenAndRejectedToken_HaveTheSameBody_OnlyTheHeaderDiffers()
    {
        // L3: süresi dolmuş ile geçersiz ayrılmaz; istemci her ikisinde oturumu kapatır.
        var noToken = await GetAsync(DefaultUrl, authorization: null);
        var expired = await GetAsync(DefaultUrl, Bearer(RejectedToken("expired")));

        Assert.Equal(await BodyWithoutTraceIdAsync(noToken), await BodyWithoutTraceIdAsync(expired));
    }

    // ---- token geçerli ----

    [Fact]
    public async Task ValidToken_PassesTheFallbackPolicy_AndActorIdIsTheSubject()
    {
        var token = _factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(UserId).Value;

        var response = await GetAsync(DefaultUrl, Bearer(token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(UserId, json.RootElement.GetProperty("actorId").GetGuid());
    }

    [Fact]
    public async Task ValidTokenWithoutTheRole_Returns403Problem_WithoutChallengeHeader()
    {
        var token = _factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(UserId).Value;

        var response = await GetAsync(AdminUrl, Bearer(token));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "auth.forbidden");
        Assert.Empty(response.Headers.WwwAuthenticate);
    }

    [Fact]
    public async Task RoleEndpoint_WithoutToken_Is401_Not403()
    {
        // Önce kimlik: tanınmayan kullanıcıya "yetkin yok" denmez.
        var response = await GetAsync(AdminUrl, authorization: null);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    // ---- fallback policy ve GetActorId ----

    [Fact]
    public void FallbackPolicy_RequiresAnAuthenticatedUser()
    {
        var fallback = _factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task UnknownRoute_WithoutToken_Returns401()
    {
        // Ölçüldü (8 Kasım): eşleşen endpoint yokken de fallback policy uygulanıyor. Anonim istemci hangi
        // adreslerin var olduğunu öğrenemez (var olan ve olmayan adres aynı 401'i alır).
        var response = await GetAsync("/api/does-not-exist", authorization: null);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    [Fact]
    public async Task UnknownRoute_WithValidToken_Returns404()
    {
        var token = _factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(UserId).Value;

        var response = await GetAsync("/api/does-not-exist", Bearer(token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestController_ExistsOnlyInHostsThatAddIt()
    {
        // Kontrol: test controller'ı başka bir host'a (ve üretime) sızmaz.
        using var plain = TestApp.Create(TestApp.UnusedConnectionString);
        using var client = plain.CreateClient();
        var token = plain.Services.GetRequiredService<ITokenService>().CreateAccessToken(UserId).Value;
        using var request = new HttpRequestMessage(HttpMethod.Get, DefaultUrl);
        request.Headers.Authorization = Bearer(token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetActorId_OnAnAnonymousEndpointWithoutUser_IsAServerError()
    {
        // Yanlış kullanım bizim hatamız: 401 gibi gösterilmez, 500 olarak görünür kalır.
        var response = await GetAsync(AnonymousActorUrl, authorization: null);

        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "system.unexpected");
    }

    // ---- yardımcılar ----

    private string RejectedToken(string kind)
    {
        var sub = new Dictionary<string, object> { ["sub"] = UserId.ToString() };
        return kind switch
        {
            "malformed" => "not-a-jwt",
            "other-key" => OtherHostToken(),
            "expired" => TestTokens.Create(_factory, sub,
                issuedAt: DateTimeOffset.UtcNow.AddHours(-2), expires: DateTimeOffset.UtcNow.AddHours(-1)),
            "no-sub" => TestTokens.Create(_factory, new Dictionary<string, object> { ["name"] = "ahmet" }),
            "non-guid-sub" => TestTokens.Create(_factory, new Dictionary<string, object> { ["sub"] = "ahmet" }),
            "empty-guid-sub" => TestTokens.Create(_factory, new Dictionary<string, object> { ["sub"] = Guid.Empty.ToString() }),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static string OtherHostToken()
    {
        using var other = TestApp.Create(TestApp.UnusedConnectionString);
        return other.Services.GetRequiredService<ITokenService>().CreateAccessToken(UserId).Value;
    }

    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);

    private async Task<HttpResponseMessage> GetAsync(string url, AuthenticationHeaderValue? authorization)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = authorization;
        return await client.SendAsync(request);
    }

    private static string WwwAuthenticate(HttpResponseMessage response) =>
        string.Join(", ", response.Headers.GetValues("WWW-Authenticate"));

    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.True(node.Remove("traceId"), "Error responses carry a traceId.");
        return node.ToJsonString();
    }

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));
        return json;
    }

    public void Dispose() => _factory.Dispose();
}
