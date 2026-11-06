using Microsoft.Extensions.Options;

namespace Halisaha.Infrastructure.Authentication;

/// <summary>
/// L6 kuralları. Host altında ValidateOnStart ile uygulama açılırken çalışır; host dışında (ör. testlerin kendi
/// DI kurulumu) <c>IOptions&lt;JwtOptions&gt;.Value</c>'ya ilk erişimde çalışır. Hatalar toplanır, hepsi birlikte
/// raporlanır. Mesajlar anahtarın değerini asla içermez; yalnızca bozulan kuralı ve anahtarın nereye yazılacağını.
/// </summary>
internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const string WhereToSetKey =
        "Development: dotnet user-secrets set \"Jwt:SigningKey\" \"<base64>\" --project src/Halisaha.Api; " +
        "other environments: the Jwt__SigningKey environment variable. Generate one with: openssl rand -base64 64.";

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add($"Jwt:SigningKey is not configured. {WhereToSetKey}");
        }
        else if (JwtTokenValidation.TryDecodeSigningKey(options.SigningKey) is not { } keyBytes)
        {
            failures.Add($"Jwt:SigningKey is not valid Base64. {WhereToSetKey}");
        }
        else if (keyBytes.Length < JwtOptions.MinSigningKeyBytes)
        {
            failures.Add(
                $"Jwt:SigningKey must decode to at least {JwtOptions.MinSigningKeyBytes} bytes, but it decodes to {keyBytes.Length}. {WhereToSetKey}");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is not configured.");
        }

        if (options.AccessTokenLifetimeMinutes is < JwtOptions.MinAccessTokenLifetimeMinutes or > JwtOptions.MaxAccessTokenLifetimeMinutes)
        {
            failures.Add(
                $"Jwt:AccessTokenLifetimeMinutes must be between {JwtOptions.MinAccessTokenLifetimeMinutes} and " +
                $"{JwtOptions.MaxAccessTokenLifetimeMinutes}, but it is {options.AccessTokenLifetimeMinutes}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
