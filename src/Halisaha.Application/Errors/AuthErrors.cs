using Halisaha.Domain.Common;

namespace Halisaha.Application.Errors;

/// <summary>
/// Kimlik (kayıt/giriş) hataları. Bir domain kuralı değil, ama Domain'deki hata sınıflarıyla aynı kalıbı izler:
/// tür, kod ve mesaj tek yerde. EmailTaken iki yoldan fırlatılır: Register handler'ı (Identity'nin DuplicateEmail
/// sonucu) ve PostgresErrorTranslator (EmailIndex yarışı, 23505). Login hataları yalnızca Login handler'ından.
/// </summary>
public static class AuthErrors
{
    public static DomainException EmailTaken() =>
        DomainException.Conflict("auth.email_taken", "An account with this email address already exists.");

    /// <summary>
    /// L3: e-posta kayıtlı değil ve parola yanlış aynı cevabı alır; hangisinin yanlış olduğu söylenmez.
    /// Gövde aynıdır, süre aynı değildir (kayıtsız e-postada hash doğrulanmaz): Karar 11, kabul edilen risk.
    /// </summary>
    public static DomainException InvalidCredentials() =>
        DomainException.Unauthenticated("auth.invalid_credentials", "Invalid email or password.");

    /// <summary>
    /// L4: hesap kilitli. Kilit parolaya bakılmadan kontrol edilir; cevap parolanın doğru olup olmadığını söylemez.
    /// Yalnızca kayıtlı hesaplarda oluşur, yani hesabın varlığını gösterir (kabul edilen risk).
    /// </summary>
    public static DomainException LockedOut() =>
        DomainException.TooManyRequests(
            "auth.locked_out", "Too many failed sign-in attempts. Please try again in a few minutes.");
}
