using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Halisaha.Api.Tests.Fixtures;
using Halisaha.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Api.Tests.OpenApi;

/// <summary>
/// OpenAPI dokümanı ve Scalar: yalnızca Development'ta ve anonim açılır; doküman Bearer şemasını ve hangi işlemin
/// token istediğini doğru söyler (uygulamadaki kuralla aynı: [AllowAnonymous] yoksa ister). Veritabanı gerekmez.
/// </summary>
public sealed class OpenApiTests
{
    private const string DocumentUrl = "/openapi/v1.json";
    private const string ScalarUrl = "/scalar";

    [Fact]
    public async Task Development_ServesTheDocumentAnonymously_WithTheBearerScheme()
    {
        using var factory = Create("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(DocumentUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var scheme = json.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
    }

    [Theory]
    [InlineData("/api/me", "get", true)]
    [InlineData("/api/auth/login", "post", false)]
    [InlineData("/api/auth/register", "post", false)]
    public async Task Document_MarksOnlyProtectedOperationsAsRequiringAToken(string path, string method, bool requiresToken)
    {
        using var factory = Create("Development");
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(DocumentUrl));
        var operation = json.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

        var schemes = operation.TryGetProperty("security", out var security)
            ? security.EnumerateArray().SelectMany(r => r.EnumerateObject().Select(p => p.Name)).ToList()
            : [];
        Assert.Equal(requiresToken, schemes.Contains("Bearer"));
    }

    [Fact]
    public async Task Document_ShowsPositionAsNames_TheOnlyFormTheApiAccepts()
    {
        // API pozisyonu yalnızca adıyla kabul eder ("Forward"); sayı (1) gönderen 400 alır (RegisterEndpointTests).
        // Doküman da aynısını söylemeli, yoksa Scalar ve Postman kullanıcısına çalışmayan bir örnek gösterir.
        using var factory = Create("Development");
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(DocumentUrl));
        var position = json.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("Position");

        Assert.True(position.TryGetProperty("enum", out var values), "Position should be documented as an enum of names.");
        Assert.Equal(["Goalkeeper", "Defender", "Midfielder", "Forward"], values.EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public async Task Development_ServesScalarAnonymously()
    {
        using var factory = Create("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ScalarUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(DocumentUrl)]
    [InlineData(ScalarUrl)]
    public async Task OutsideDevelopment_TheApiMapIsNotServed(string url)
    {
        // Testing ortamı (Production gibi): adresler hiç yok. Anonim istek 401 alır (fallback policy, var olan ve
        // olmayan adres ayırt edilmez); giriş yapmış istek 404.
        using var factory = Create("Testing");
        using var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(Guid.NewGuid()).Value;

        var anonymous = await client.GetAsync(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var authenticated = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, authenticated.StatusCode);
    }

    private static WebApplicationFactory<Program> Create(string environment) =>
        TestApp.Create(TestApp.UnusedConnectionString, builder => builder.UseEnvironment(environment));
}
