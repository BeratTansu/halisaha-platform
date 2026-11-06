using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Halisaha.Api.Contracts.Auth;
using Halisaha.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Halisaha.Api.Tests.Auth;

/// <summary>
/// <c>POST /api/auth/login</c> uçtan uca (L3, L4, L7): HTTP sözleşmesi, hata eşlemesi, kilit ve üretilen token'ın
/// uygulamanın kendi doğrulamasından geçmesi. Kilit ve sayacın ayrıntısı Infrastructure testlerinde, handler'ın
/// akışı Application testlerinde. Kullanıcılar gerçek register endpoint'iyle oluşturulur: kayıtta oluşan hesabın
/// girişte bulunduğu (e-posta normalizasyonu, LockoutEnabled) da böylece kanıtlanır.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LoginEndpointTests(ApiFixture fixture)
{
    private const string LoginUrl = "/api/auth/login";
    private const string RegisterUrl = "/api/auth/register";
    private const string Password = "halisaha1";
    private const string WrongPassword = "yanlis-parola1";

    // Kodlar bilerek string: istemcinin gördüğü sözleşme (RegisterEndpointTests ile aynı ilke).

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task ValidCredentials_Return200WithAuthResponseOnly()
    {
        var (email, userId) = await RegisterAsync(_client);

        var response = await LoginAsync(_client, email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        // L7: tam olarak bu üç alan; ad, e-posta, profil yok (/api/me'den gelir), tokenType yok.
        Assert.Equal(["accessToken", "expiresIn", "userId"], json.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetGuid());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal(60 * 60, json.RootElement.GetProperty("expiresIn").GetInt32()); // appsettings: 60 dk, saniye
    }

    [Fact]
    public async Task SuccessfulResponse_IsNotCached()
    {
        var (email, _) = await RegisterAsync(_client);

        var response = await LoginAsync(_client, email, Password);

        Assert.True(response.Headers.CacheControl?.NoStore, "A response carrying a token must not be stored by caches.");
    }

    [Fact]
    public async Task TokenFromLogin_IsAuthenticatedByTheAppsOwnBearerHandler()
    {
        // Login ve doğrulama aynı host'ta: her factory kendi rastgele anahtarını alır.
        await using var factory = fixture.CreateFactory(AuthenticateProbe.AddTo);
        using var client = factory.CreateClient();
        var (email, userId) = await RegisterAsync(client);

        var token = await ReadTokenAsync(await LoginAsync(client, email, Password));
        var probe = await AuthenticateProbe.AuthenticateAsync(client, token);

        Assert.True(probe.Succeeded, probe.Failure);
        Assert.Equal(userId.ToString(), probe.Name); // sub = login olan kullanıcı
    }

    [Fact]
    public async Task EmailWithSurroundingSpacesAndOtherCasing_SignsIn()
    {
        var (email, userId) = await RegisterAsync(_client);

        var response = await LoginAsync(_client, $"  {email.ToUpperInvariant()} ", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task WrongPassword_Returns401InvalidCredentials_WithBearerChallenge()
    {
        var (email, _) = await RegisterAsync(_client);

        var response = await LoginAsync(_client, email, WrongPassword);

        using var json = await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
        Assert.False(json.RootElement.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task UnknownEmail_AndWrongPassword_ReturnSameStatusCodeAndBody()
    {
        // Yalnızca içerik aynı; süre aynı değil (kayıtsız e-postada hash doğrulanmaz). Zamanlama farkı Karar 11'de
        // kabul edilen risk; bu test onu kanıtlamaz, iddia da etmez.
        var (email, _) = await RegisterAsync(_client);

        var wrongPassword = await LoginAsync(_client, email, WrongPassword);
        var unknownEmail = await LoginAsync(_client, UniqueEmail(), WrongPassword);

        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        Assert.Equal(await BodyWithoutTraceIdAsync(wrongPassword), await BodyWithoutTraceIdAsync(unknownEmail));
    }

    [Fact]
    public async Task FifthWrongPassword_Returns429LockedOut_AndTheCorrectPasswordIsRejectedWhileLocked()
    {
        var (email, _) = await RegisterAsync(_client);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await AssertProblemAsync(await LoginAsync(_client, email, WrongPassword), HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        }

        await AssertProblemAsync(await LoginAsync(_client, email, WrongPassword), (HttpStatusCode)429, "auth.locked_out");
        using var locked = await AssertProblemAsync(await LoginAsync(_client, email, Password), (HttpStatusCode)429, "auth.locked_out");
        Assert.False(locked.RootElement.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task EmptyFields_Return400ValidationFailed_WithCamelCaseFields()
    {
        var response = await _client.PostAsJsonAsync(LoginUrl, new { email = "  ", password = "" });

        using var json = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal(["email", "password"], json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{ bozuk""")]
    public async Task UnreadableBody_Returns400InTheSameShape(string body)
    {
        var response = await _client.PostAsync(LoginUrl, new StringContent(body, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task SignInFlows_NeverWritePasswordsOrEmailsToTheLog()
    {
        // Bütün kategoriler Trace seviyesinde yakalanır: bugün kapalı olan bir ayrıntı (ör. EF parametre
        // değerleri, HTTP gövde logu) yarın açılırsa bu test kırmızı olur. Değerler testin kendine özgüdür;
        // başka bir yerden tesadüfen log'a düşmüş olamaz.
        var email = $"sentinel.{Guid.NewGuid():N}@test.local";
        var unknownEmail = $"sentinel.unknown.{Guid.NewGuid():N}@test.local";
        const string password = "sentinelParola7f3a";
        const string wrongPassword = "sentinelYanlis9c2e";
        var logs = new ListLoggerProvider();
        await using var factory = fixture.CreateFactory(builder => builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(logs);
            logging.AddFilter<ListLoggerProvider>(category: null, LogLevel.Trace);
        }));
        using var client = factory.CreateClient();

        await RegisterAsync(client, email, password);                                   // 201
        await LoginAsync(client, email, password);                                       // 200
        await LoginAsync(client, unknownEmail, wrongPassword);                           // 401, kayıtsız
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(client, email, wrongPassword);                              // 401 x4, sonra 429
        }
        await LoginAsync(client, email, password);                                       // 429, kilitli
        await client.PostAsJsonAsync(LoginUrl, new { email = "", password });            // 400, validation
        await client.PostAsync(LoginUrl, new StringContent(                              // 400, bozuk JSON
            $$"""{"email":"{{email}}","password":"{{password}}",""", Encoding.UTF8, "application/json"));

        // Yakalamanın gerçekten çalıştığının kanıtı: ayrıntılı kayıtlar ve SQL komutları da log'da.
        Assert.Contains(logs.Entries, e => e.Level <= LogLevel.Debug);
        Assert.Contains(logs.Entries, e => e.Category == "Microsoft.EntityFrameworkCore.Database.Command");

        string[] secrets = [password, wrongPassword, email, unknownEmail, email.ToUpperInvariant(), unknownEmail.ToUpperInvariant()];
        var leaks = logs.Entries
            .Where(e => secrets.Any(s =>
                e.Message.Contains(s, StringComparison.OrdinalIgnoreCase)
                || e.State.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (e.Exception?.ToString().Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)))
            .Select(e => $"{e.Category} [{e.Level}]: {e.Message}")
            .ToList();
        Assert.True(leaks.Count == 0, "Sensitive values leaked into the log:\n" + string.Join("\n", leaks));
    }

    [Fact]
    public void LoginRequestAndAuthResponse_ToStringDoNotRevealSecrets()
    {
        var request = new LoginRequest("ahmet@example.com", "cok-gizli-parola-42");
        var response = new AuthResponse(Guid.NewGuid(), "eyJ.gizli.token", 3600);

        Assert.DoesNotContain("cok-gizli-parola-42", request.ToString());
        Assert.DoesNotContain("ahmet@example.com", request.ToString()); // MVC argümanları Trace'te ToString ile loglar
        Assert.DoesNotContain("eyJ.gizli.token", response.ToString());
    }

    // ---- yardımcılar ----

    private async Task<(string Email, Guid UserId)> RegisterAsync(HttpClient client, string? email = null, string password = Password)
    {
        email ??= UniqueEmail();
        var response = await client.PostAsJsonAsync(RegisterUrl, new
        {
            email,
            password,
            firstName = "Ahmet",
            lastName = "Yılmaz",
            preferredPosition = "Forward",
            skillLevel = 3,
            preferredDistrictId = fixture.SeededDistrictId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return (email, json.RootElement.GetProperty("userId").GetGuid());
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(LoginUrl, new { email, password });

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<string> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.True(node.Remove("traceId"), "Error responses carry a traceId.");
        return node.ToJsonString();
    }

    /// <summary>Bütün hata cevaplarının ortak biçimi: problem+json, status, code ve traceId.</summary>
    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await ReadJsonAsync(response);
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));
        return json;
    }
}
