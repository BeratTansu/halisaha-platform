namespace Halisaha.Application.Abstractions.Identity;

public enum CredentialVerificationStatus
{
    Succeeded,

    /// <summary>E-posta kayıtlı değil ya da parola yanlış. İkisi bilerek ayrılmaz (L3).</summary>
    InvalidCredentials,

    /// <summary>Hesap kilitli (L4). Parola kontrol edilmeden döner.</summary>
    LockedOut,
}

/// <summary>
/// Giriş bilgilerini doğrulamanın beklenen sonuçları. <see cref="IdentityCreationResult"/> ile aynı kalıp: yalnızca
/// handler'ın kullanıcıya anlamlı bir cevaba çevirebileceği durumlar; beklenmeyenler exception olarak kalır.
/// </summary>
public sealed class CredentialVerificationResult
{
    private CredentialVerificationResult(CredentialVerificationStatus status, Guid userId)
    {
        Status = status;
        UserId = userId;
    }

    public CredentialVerificationStatus Status { get; }

    /// <summary>Yalnızca <see cref="CredentialVerificationStatus.Succeeded"/> için dolu; diğerlerinde <see cref="Guid.Empty"/>.</summary>
    public Guid UserId { get; }

    public static CredentialVerificationResult InvalidCredentials { get; } =
        new(CredentialVerificationStatus.InvalidCredentials, Guid.Empty);

    public static CredentialVerificationResult LockedOut { get; } =
        new(CredentialVerificationStatus.LockedOut, Guid.Empty);

    public static CredentialVerificationResult Succeeded(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A successful verification must carry the user's id.", nameof(userId));
        }

        return new CredentialVerificationResult(CredentialVerificationStatus.Succeeded, userId);
    }
}
