using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Halisaha.Api.Tests.Fixtures;

/// <summary>
/// Testlerin uygulama host'u: ortam "Testing" (appsettings.Development.json ve user-secrets yüklenmez),
/// bağlantı ve JWT anahtarı yalnızca buradan gelir. Veritabanı gerektirmeyen testler (ör. açılış doğrulaması,
/// token doğrulama ayarı) kullanılmayan bir bağlantı ile doğrudan bunu çağırır; uygulama açılırken DB'ye bağlanmaz.
/// </summary>
public static class TestApp
{
    /// <summary>Program.cs yalnızca boş olmadığını kontrol eder; bu adrese hiç bağlanılmaz.</summary>
    public const string UnusedConnectionString = "Host=unused.invalid;Database=unused";

    public static WebApplicationFactory<Program> Create(string connectionString, Action<IWebHostBuilder>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            // UseSetting, Program.cs'in builder.Build()'den önceki okumasına (fail-fast kontrolü) ulaşır.
            builder.UseSetting("ConnectionStrings:Halisaha", connectionString);
            // Her host kendi rastgele anahtarını alır (L6): repoda bilinen bir test anahtarı durmaz.
            // Issuer, audience ve süre uygulamanın appsettings.json'ından gelir.
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
            // Test sunucusu istemci IP'si vermez: bütün testler tek kovaya (unknown) düşer ve ortak host'ta kota
            // dolardı. Limit burada pratikte sınırsız; limitin kendisi AuthRateLimitingTests'te düşük limitle denenir.
            builder.UseSetting("RateLimiting:Auth:PermitLimit", int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            configure?.Invoke(builder);
        });
}
