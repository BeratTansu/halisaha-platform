namespace Halisaha.Application.Abstractions.Identity;

public enum IdentityCreationStatus
{
    Succeeded,

    /// <summary>Bu e-postayla bir hesap zaten var (normal yol; yarıştaki 23505 exception olarak gelir).</summary>
    EmailTaken,

    /// <summary>Kimlik sistemi girdiyi reddetti (parola politikası, e-posta biçimi); ayrıntı <see cref="IdentityCreationResult.Errors"/>'ta.</summary>
    Rejected,
}

/// <summary>
/// Kimlik oluşturmanın beklenen sonuçları. Yalnızca handler'ın kullanıcıya anlamlı bir cevaba çevirebileceği
/// durumları taşır; beklenmeyen durumlar bu tipe çevrilip gizlenmez, exception olarak kalır.
/// </summary>
public sealed class IdentityCreationResult
{
    private IdentityCreationResult(IdentityCreationStatus status, IReadOnlyList<IdentityFieldError> errors)
    {
        Status = status;
        Errors = errors;
    }

    public IdentityCreationStatus Status { get; }

    /// <summary>Yalnızca <see cref="IdentityCreationStatus.Rejected"/> için dolu.</summary>
    public IReadOnlyList<IdentityFieldError> Errors { get; }

    public static IdentityCreationResult Success { get; } = new(IdentityCreationStatus.Succeeded, []);

    public static IdentityCreationResult EmailTaken { get; } = new(IdentityCreationStatus.EmailTaken, []);

    public static IdentityCreationResult Rejected(IReadOnlyList<IdentityFieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("A rejection must carry at least one error.", nameof(errors));
        }

        return new IdentityCreationResult(IdentityCreationStatus.Rejected, errors);
    }
}
