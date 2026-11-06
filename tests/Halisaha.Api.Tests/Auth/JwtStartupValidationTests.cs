using Halisaha.Api.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Halisaha.Api.Tests.Auth;

/// <summary>
/// Fail-fast (L6): JWT ayarı bozuksa uygulama açılmaz. Kuralların kendisi Infrastructure testlerinde;
/// burada uygulamanın onları açılışta gerçekten çalıştırdığı (ValidateOnStart) kanıtlanır. ValidateOnStart
/// olmasaydı uygulama açılır, hata ilk login'de (IDX10720) ya da her doğrulamada (herkes 401) çıkardı.
/// Veritabanı gerekmez.
/// </summary>
public sealed class JwtStartupValidationTests
{
    [Fact]
    public void ValidSettings_AppStarts()
    {
        using var factory = TestApp.Create(TestApp.UnusedConnectionString);

        using var client = factory.CreateClient(); // host burada başlar

        Assert.NotNull(client);
    }

    [Theory]
    [InlineData("Jwt:SigningKey", "", "Jwt:SigningKey is not configured")]
    [InlineData("Jwt:SigningKey", "c2hvcnQ=", "at least 32 bytes")] // "short": geçerli Base64, 5 bayt
    [InlineData("Jwt:AccessTokenLifetimeMinutes", "15.00:00:00", "Jwt:AccessTokenLifetimeMinutes")]
    [InlineData("Jwt:AccessTokenLifetimeMinutes", "0", "between 1 and 60")]
    [InlineData("Jwt:Issuer", "", "Jwt:Issuer is not configured")]
    public void InvalidSetting_AppDoesNotStart(string key, string value, string expectedMessage)
    {
        using var factory = TestApp.Create(TestApp.UnusedConnectionString, builder => builder.UseSetting(key, value));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        // Bağlama hatası (int alana TimeSpan metni) da açılışı durdurur; mesajı validator'ınki değil binder'ınkidir.
        var messages = Flatten(exception).Select(e => e is OptionsValidationException v ? string.Join(" ", v.Failures) : e.Message);
        Assert.Contains(messages, m => m.Contains(expectedMessage));
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
                {
                    yield return inner;
                }
            }
        }
    }
}
