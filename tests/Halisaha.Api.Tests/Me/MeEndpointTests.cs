using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Halisaha.Api.Contracts.Me;
using Halisaha.Api.Tests.Fixtures;
using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Halisaha.Api.Tests.Me;

/// <summary>
/// <c>GET /api/me</c> uçtan uca (L7 + auth sözleşmesi): cevabın alanları, profilsiz kullanıcı, sahibi olmayan
/// token ve "bütün istek tek SQL komutu" iddiası. Kullanıcılar gerçek register ve login endpoint'leriyle oluşturulur.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MeEndpointTests(ApiFixture fixture)
{
    private const string MeUrl = "/api/me";
    private const string Password = "halisaha1";

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task PlayerWithToken_Gets200_WithExactlyTheContractFields()
    {
        var (email, userId, token) = await RegisterAndLoginAsync(_client, phoneNumber: "+90 555 123 45 67");

        var response = await GetMeAsync(_client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(["email", "firstName", "lastName", "phoneNumber", "profile", "userId"], Names(root));
        Assert.Equal(userId, root.GetProperty("userId").GetGuid());
        Assert.Equal(email, root.GetProperty("email").GetString());
        Assert.Equal("Ahmet", root.GetProperty("firstName").GetString());
        Assert.Equal("Yılmaz", root.GetProperty("lastName").GetString());
        Assert.Equal("+90 555 123 45 67", root.GetProperty("phoneNumber").GetString());

        var profile = root.GetProperty("profile");
        Assert.Equal(["district", "preferredPosition", "province", "skillLevel"], Names(profile));
        Assert.Equal("Forward", profile.GetProperty("preferredPosition").GetString()); // enum metin olarak
        Assert.Equal(3, profile.GetProperty("skillLevel").GetInt32());

        var (districtName, provinceId, provinceName) = await fixture.QueryAsync(context => context.Districts
            .Where(d => d.Id == fixture.SeededDistrictId)
            .Select(d => new ValueTuple<string, int, string>(d.Name, d.ProvinceId, d.Province!.Name))
            .SingleAsync());
        Assert.Equal(["id", "name"], Names(profile.GetProperty("district")));
        Assert.Equal(fixture.SeededDistrictId, profile.GetProperty("district").GetProperty("id").GetInt32());
        Assert.Equal(districtName, profile.GetProperty("district").GetProperty("name").GetString());
        Assert.Equal(provinceId, profile.GetProperty("province").GetProperty("id").GetInt32());
        Assert.Equal(provinceName, profile.GetProperty("province").GetProperty("name").GetString());
    }

    [Fact]
    public async Task MissingPhoneNumber_IsWrittenAsNull()
    {
        var (_, _, token) = await RegisterAndLoginAsync(_client, phoneNumber: null);

        using var json = JsonDocument.Parse(await (await GetMeAsync(_client, token)).Content.ReadAsStringAsync());

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("phoneNumber").ValueKind);
    }

    [Fact]
    public async Task UserWithoutPlayerProfile_GetsProfileNull()
    {
        // Oyuncu olmayan kullanıcı (ileride Admin): API'de bu kullanıcıyı oluşturan bir endpoint henüz yok;
        // uygulamanın kendi servisleriyle profilsiz oluşturulur, token'ı uygulamanın kendi servisinden alınır.
        var userId = Guid.NewGuid();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                var result = await identity.CreateUserAsync(
                    new NewIdentityUser(userId, UniqueEmail(), "Admin", "Kullanıcı", null, DateTimeOffset.UtcNow), Password);
                Assert.Equal(IdentityCreationStatus.Succeeded, result.Status);
            }, CancellationToken.None);
        }
        var token = fixture.Factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(userId).Value;

        var response = await GetMeAsync(_client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("profile").ValueKind); // alan var, değeri null
    }

    [Fact]
    public async Task ValidTokenForAUserWhoDoesNotExist_Is401_WithInvalidTokenChallenge()
    {
        // Auth sözleşmesi (Karar 11): imza ve süre geçerli, ama sahibi sistemde yok. 404 değil (adres var, eksik
        // olan kimlik), 403 değil (istemci oturumu kapatmalı). Başlık: token gönderildi ve reddedildi.
        var token = fixture.Factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(Guid.NewGuid()).Value;

        var response = await GetMeAsync(_client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("auth.unauthenticated", json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal("Bearer error=\"invalid_token\"", string.Join(", ", response.Headers.GetValues("WWW-Authenticate")));
    }

    [Fact]
    public async Task NoToken_Is401_EvenThoughTheControllerHasNoAuthorizeAttribute()
    {
        // MeController'da [Authorize] yok: korumayı fallback policy sağlıyor.
        var response = await GetMeAsync(_client, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", string.Join(", ", response.Headers.GetValues("WWW-Authenticate")));
    }

    [Fact]
    public async Task WholeRequest_RunsExactlyOneSqlCommand()
    {
        // LINQ'tan varsayılmaz, sayılır: kimlik doğrulama dahil bütün istek boyunca veritabanına giden komutlar.
        var logs = new ListLoggerProvider();
        await using var factory = fixture.CreateFactory(builder => builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(logs);
            logging.AddFilter<ListLoggerProvider>(category: null, LogLevel.Information);
        }));
        using var client = factory.CreateClient();
        var (_, _, token) = await RegisterAndLoginAsync(client, phoneNumber: null);
        var before = ExecutedCommands(logs).Count;

        var response = await GetMeAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.Single(ExecutedCommands(logs).Skip(before));
        Assert.Contains("LEFT JOIN \"PlayerProfiles\"", command);
    }

    [Fact]
    public void Response_ToString_DoesNotRevealPersonalData()
    {
        var response = new MeResponse(Guid.NewGuid(), "ahmet@example.com", "Ahmet", "Yılmaz", "+90 555 123 45 67", null);

        Assert.DoesNotContain("ahmet@example.com", response.ToString());
        Assert.DoesNotContain("Yılmaz", response.ToString());
    }

    // ---- yardımcılar ----

    private async Task<(string Email, Guid UserId, string Token)> RegisterAndLoginAsync(HttpClient client, string? phoneNumber)
    {
        var email = UniqueEmail();
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = Password,
            firstName = "Ahmet",
            lastName = "Yılmaz",
            phoneNumber,
            preferredPosition = "Forward",
            skillLevel = 3,
            preferredDistrictId = fixture.SeededDistrictId,
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return (email, json.RootElement.GetProperty("userId").GetGuid(), json.RootElement.GetProperty("accessToken").GetString()!);
    }

    private static async Task<HttpResponseMessage> GetMeAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    private static List<string> ExecutedCommands(ListLoggerProvider logs) => logs.Entries
        .Where(e => e.Category == "Microsoft.EntityFrameworkCore.Database.Command"
                    && e.Message.StartsWith("Executed DbCommand", StringComparison.Ordinal))
        .Select(e => e.Message)
        .ToList();

    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(p => p.Name).Order()];

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";
}
