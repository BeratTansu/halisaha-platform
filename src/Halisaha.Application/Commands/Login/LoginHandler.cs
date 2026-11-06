using FluentValidation;
using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Errors;

namespace Halisaha.Application.Commands.Login;

/// <summary>
/// Login use case'i: giriş bilgilerini doğrular, başarılıysa access token üretir. Transaction yok: tek yazma
/// (başarısız deneme sayacı) kimlik sisteminin içindedir ve hemen kalıcı olmalıdır (bkz. IIdentityService).
/// </summary>
public sealed class LoginHandler(
    IValidator<LoginCommand> validator,
    IIdentityService identity,
    ITokenService tokens)
{
    public async Task<AuthResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Önce normalize, sonra doğrula (Register ile aynı sıra).
        var normalized = command.Normalize();

        // 2. İsteğin şekli (400).
        await validator.ValidateAndThrowAsync(normalized, cancellationToken);

        // 3. Kimlik doğrulama. İptal burada kontrol edilir: doğrulama başladıktan sonra iptal edilemez (port
        //    token almıyor); yarıda kalan bir istek sayacı yine de doğru günceller.
        cancellationToken.ThrowIfCancellationRequested();
        var result = await identity.VerifyCredentialsAsync(normalized.Email, normalized.Password);

        // 4. Token yalnızca doğrulanmış kullanıcıya, doğrulamadan SONRA üretilir.
        return result.Status switch
        {
            CredentialVerificationStatus.Succeeded => new AuthResult(result.UserId, tokens.CreateAccessToken(result.UserId)),
            CredentialVerificationStatus.InvalidCredentials => throw AuthErrors.InvalidCredentials(), // 401
            CredentialVerificationStatus.LockedOut => throw AuthErrors.LockedOut(),                   // 429
            _ => throw new InvalidOperationException($"Unknown credential verification status '{result.Status}'."),
        };
    }
}
