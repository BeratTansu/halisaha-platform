using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Halisaha.Api.Tests.Fixtures;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Halisaha.Api.Tests.Auth;

/// <summary>
/// <c>POST /api/auth/register</c> uçtan uca: HTTP sözleşmesi (status, gövde biçimi, alan adları), hata eşlemesi
/// ve veritabanına yazılanlar. Transaction'ın kendisi Infrastructure testlerinde, handler'ın sırası Application
/// testlerinde kanıtlandı; burada katmanların birbirine doğru bağlandığı kanıtlanır.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RegisterEndpointTests(ApiFixture fixture)
{
    private const string Url = "/api/auth/register";
    private const string ValidPassword = "halisaha1";

    // Hata kodları ve alan adları bilerek string: bunlar istemcinin gördüğü sözleşme. Koddaki bir sabit
    // değişirse testin de onunla birlikte sessizce değişmesi değil, kırılması istenir.

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task ValidRequest_Returns201WithAuthResponse_AndPersistsEveryField()
    {
        var email = UniqueEmail();
        var body = ValidBody(email) with
        {
            FirstName = " Ahmet ",
            PhoneNumber = "+90 555 123 45 67",
            PreferredPosition = "Midfielder",
            SkillLevel = 4,
        };

        var response = await _client.PostAsJsonAsync(Url, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location); // bilinçli: kullanıcıyı getiren bir GET endpoint'i henüz yok
        using var json = await ReadJsonAsync(response);
        // L7: login ile aynı gövde. userId korundu, mevcut istemciler bozulmaz.
        Assert.Equal(["accessToken", "expiresIn", "userId"], json.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal(60 * 60, json.RootElement.GetProperty("expiresIn").GetInt32());
        var userId = json.RootElement.GetProperty("userId").GetGuid();

        // Request → command → handler zinciri hiçbir alanı kaybetmedi; kullanıcı ve profil birlikte yazıldı.
        var (user, profile) = await fixture.QueryAsync(async db => (
            await db.Users.SingleAsync(u => u.Id == userId),
            await db.PlayerProfiles.SingleAsync(p => p.UserId == userId)));
        Assert.Equal(email, user.Email);
        Assert.Equal("Ahmet", user.FirstName);            // normalize edildi
        Assert.Equal(body.LastName, user.LastName);
        Assert.Equal(body.PhoneNumber, user.PhoneNumber);
        Assert.Equal(userId.ToString(), user.UserName);
        Assert.NotEqual(default, user.CreatedAt);          // TimeProvider bağlı
        Assert.Equal(Position.Midfielder, profile.PreferredPosition);
        Assert.Equal(4, profile.SkillLevel);
        Assert.Equal(fixture.SeededDistrictId, profile.PreferredDistrictId);
    }

    [Theory]
    [InlineData("Goalkeeper")]
    [InlineData("Defender")]
    [InlineData("Midfielder")]
    [InlineData("Forward")]
    public async Task EveryPositionName_IsPartOfTheContract(string position)
    {
        // Pozisyon adları JSON sözleşmesinin parçası ve Domain enum'undan gelir: enum üyesi yeniden
        // adlandırılırsa istemciler sessizce bozulmasın, bu test kırılsın.
        var response = await _client.PostAsJsonAsync(Url, ValidBody() with { PreferredPosition = position });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task InvalidShape_Returns400ValidationFailedWithCamelCaseFields()
    {
        var response = await _client.PostAsJsonAsync(Url, ValidBody() with { Email = "not-an-email", FirstName = "" });

        using var json = await AssertValidationProblemAsync(response);
        Assert.Equal(["email", "firstName"], ErrorKeys(json));
    }

    [Fact]
    public async Task PasswordRejectedByIdentity_Returns400InTheSameShape_AndWritesNothing()
    {
        var email = UniqueEmail();

        var response = await _client.PostAsJsonAsync(Url, ValidBody(email) with { Password = "abcdefgh" });

        using var json = await AssertValidationProblemAsync(response);
        Assert.Equal(["password"], ErrorKeys(json));
        Assert.False(await fixture.QueryAsync(db => db.Users.AnyAsync(u => u.Email == email)));
    }

    [Theory]
    [InlineData("""{"preferredPosition":"Kaleci"}""", "preferredPosition")] // tanımsız enum metni
    [InlineData("""{"preferredPosition":1}""", "preferredPosition")]        // sayı kabul edilmez
    [InlineData("""{"skillLevel":"üç"}""", "skillLevel")]                   // yanlış tip
    [InlineData("""{ bozuk""", "body")]                                     // bozuk JSON
    [InlineData("", "body")]                                                // boş gövde
    public async Task UnreadableBody_Returns400InTheSameShape_WithoutInternalDetails(string body, string expectedKey)
    {
        var response = await _client.PostAsync(Url, new StringContent(body, Encoding.UTF8, "application/json"));

        using var json = await AssertValidationProblemAsync(response);
        Assert.Equal([expectedKey], ErrorKeys(json)); // "request" anahtarı yok, JSON yolu ("$.") yok
        var raw = json.RootElement.GetRawText();
        Assert.DoesNotContain("Halisaha.", raw);        // iç tip adı sızmıyor
        Assert.DoesNotContain("BytePositionInLine", raw);
    }

    [Fact]
    public async Task UnknownDistrict_Returns400WithDomainCode()
    {
        var response = await _client.PostAsJsonAsync(Url, ValidBody() with { PreferredDistrictId = 999_999_999 });

        using var json = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "profile.district_not_found");
        Assert.False(json.RootElement.TryGetProperty("errors", out _)); // alan haritası değil, domain kodu
    }

    [Fact]
    public async Task SkillLevelOutOfRange_Returns400WithDomainCode()
    {
        var response = await _client.PostAsJsonAsync(Url, ValidBody() with { SkillLevel = 0 });

        using var json = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "profile.invalid_skill_level");
    }

    [Fact]
    public async Task EmailAlreadyRegistered_WithDifferentCasing_Returns409EmailTaken()
    {
        var email = UniqueEmail();
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync(Url, ValidBody(email))).StatusCode);

        var response = await _client.PostAsJsonAsync(Url, ValidBody(email.ToUpperInvariant()));

        using var json = await AssertProblemAsync(response, HttpStatusCode.Conflict, "auth.email_taken");
        var normalized = email.ToUpperInvariant();
        Assert.Equal(1, await fixture.QueryAsync(db => db.Users.CountAsync(u => u.NormalizedEmail == normalized)));
    }

    [Fact]
    public async Task SuccessfulResponse_IsNotCached()
    {
        // Gövdede token var: login'deki gibi hiçbir önbellekte saklanmamalı.
        var response = await _client.PostAsJsonAsync(Url, ValidBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore, "A response carrying a token must not be stored by caches.");
    }

    [Fact]
    public async Task TokenFromRegister_SignsInImmediately()
    {
        // Otomatik girişin kanıtı: register'ın token'ıyla login olmadan /api/me çalışır ve aynı kullanıcıyı döner.
        var response = await _client.PostAsJsonAsync(Url, ValidBody());
        using var json = await ReadJsonAsync(response);
        var userId = json.RootElement.GetProperty("userId").GetGuid();
        var token = json.RootElement.GetProperty("accessToken").GetString();

        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        me.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var meResponse = await _client.SendAsync(me);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        using var meJson = await ReadJsonAsync(meResponse);
        Assert.Equal(userId, meJson.RootElement.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task InvalidBearerToken_DoesNotBlockAnonymousRegister()
    {
        // UseAuthentication pipeline'da: bozuk bir token anonim endpoint'i engellememeli (eski bir oturumdan
        // kalan token'la kayıt olan istemci). Handler token'ı reddeder ama [AllowAnonymous] endpoint çalışır.
        using var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(ValidBody()) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not.a.token");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedFailure_Returns500WithoutDetails_AndIsLoggedAsError()
    {
        const string secret = "internal-detail-that-must-not-leak";
        var logs = new ListLoggerProvider();
        await using var factory = CreateFactoryWhereDistrictLookupThrows(new InvalidOperationException(secret), logs);

        var response = await factory.CreateClient().PostAsJsonAsync(Url, ValidBody());

        using var json = await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "system.unexpected");
        var raw = json.RootElement.GetRawText();
        Assert.DoesNotContain(secret, raw);
        Assert.False(json.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain("   at ", raw); // stack trace yok
        Assert.Contains(logs.Entries, e =>
            e.Category == "Halisaha.Api.Errors.ApiExceptionHandler"
            && e.Level == LogLevel.Error
            && e.Exception?.Message == secret);
    }

    [Fact]
    public async Task DomainExceptionWithUnmappedKind_Returns500WithoutItsMessageOrCode()
    {
        // DomainErrorKind'e yeni bir değer eklenip API'de eşlenmezse: bizim hatamız. Bilinen türlerin aksine
        // mesajı ve kodu istemciye verilmez; genel 500 sözleşmesi geçerlidir ve Error olarak loglanır.
        const string secret = "message-of-an-unmapped-domain-error";
        var logs = new ListLoggerProvider();
        var unmapped = new DomainException((DomainErrorKind)999, "test.unmapped_kind", secret);
        await using var factory = CreateFactoryWhereDistrictLookupThrows(unmapped, logs);

        var response = await factory.CreateClient().PostAsJsonAsync(Url, ValidBody());

        using var json = await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "system.unexpected");
        var raw = json.RootElement.GetRawText();
        Assert.DoesNotContain(secret, raw);
        Assert.DoesNotContain("test.unmapped_kind", raw);
        Assert.False(json.RootElement.TryGetProperty("detail", out _));
        Assert.Contains(logs.Entries, e =>
            e.Category == "Halisaha.Api.Errors.ApiExceptionHandler"
            && e.Level == LogLevel.Error
            && ReferenceEquals(e.Exception, unmapped));
    }

    [Fact]
    public void RegisterRequest_ToStringDoesNotContainThePasswordOrPersonalData()
    {
        // MVC action argümanlarını Trace seviyesinde ToString ile loglar (LoginEndpointTests'teki log testi).
        var request = new Contracts.Auth.RegisterRequest(
            "ahmet@example.com", "cok-gizli-parola-42", "Ahmet", "Yılmaz", "+90 555 123 45 67", Position.Forward, 3, 1);

        var text = request.ToString();

        Assert.DoesNotContain("cok-gizli-parola-42", text);
        Assert.DoesNotContain("ahmet@example.com", text);
        Assert.DoesNotContain("Ahmet", text);
        Assert.DoesNotContain("Yılmaz", text);
        Assert.DoesNotContain("555", text);
        Assert.Contains("PreferredDistrictId = 1", text); // kişisel olmayan alanlar teşhis için görünür
    }

    // ---- yardımcılar ----

    /// <summary>JSON gövdesi istemcinin göreceği gibi: pozisyon metin, alan adları camelCase.</summary>
    private sealed record Body(
        string? Email, string? Password, string? FirstName, string? LastName, string? PhoneNumber,
        string PreferredPosition, int SkillLevel, int PreferredDistrictId);

    private Body ValidBody(string? email = null) => new(
        email ?? UniqueEmail(), ValidPassword, "Ahmet", "Yılmaz", null, "Goalkeeper", 3, fixture.SeededDistrictId);

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string[] ErrorKeys(JsonDocument json) =>
        json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToArray();

    private static Task<JsonDocument> AssertValidationProblemAsync(HttpResponseMessage response) =>
        AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");

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

    /// <summary>Handler'ın ilk port çağrısı exception fırlatır: hata eşlemesini uçtan uca, ayrı bir host'ta dener.</summary>
    private WebApplicationFactory<Program> CreateFactoryWhereDistrictLookupThrows(Exception exception, ListLoggerProvider logs) =>
        fixture.CreateFactory(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(services =>
                services.AddScoped<ILocationQueries>(_ => new ThrowingLocationQueries(exception)));
        });

    private sealed class ThrowingLocationQueries(Exception exception) : ILocationQueries
    {
        public Task<bool> DistrictExistsAsync(int districtId, CancellationToken cancellationToken) => Task.FromException<bool>(exception);
    }
}
