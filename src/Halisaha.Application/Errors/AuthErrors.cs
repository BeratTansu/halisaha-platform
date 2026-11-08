using Halisaha.Domain.Common;

namespace Halisaha.Application.Errors;

/// <summary>
/// Kimlik (kayıt/giriş) hataları. Bir domain kuralı değil, ama Domain'deki hata sınıflarıyla aynı kalıbı izler:
/// tür, kod ve mesaj tek yerde. EmailTaken iki yoldan fırlatılır: Register handler'ı (Identity'nin DuplicateEmail
/// sonucu) ve PostgresErrorTranslator (EmailIndex yarışı, 23505). Login hataları yalnızca Login handler'ından.
/// </summary>
public static class AuthErrors
{
    public const string UnauthenticatedCode = "auth.unauthenticated";
    public const string ForbiddenCode = "auth.forbidden";

    /// <summary>
    /// L3 + auth sözleşmesi (Karar 11): "bu token ile seni tanıyamıyorum". Token yok, geçersiz, süresi dolmuş ya da
    /// sahibi artık sistemde yok: hepsi aynı cevabı alır, istemcinin tek kuralı oturumu kapatmaktır. API iki yerden
    /// üretir: JwtBearer challenge'ı (token kendisi geçersiz) ve aktörü bulamayan handler'lar (bu fabrika).
    /// </summary>
    public static DomainException Unauthenticated() =>
        DomainException.Unauthenticated(UnauthenticatedCode, "Authentication is required.");

    /// <summary>Kimlik tamam, yetki yok (L3). İstemci oturumu kapatmaz.</summary>
    public static DomainException Forbidden() =>
        DomainException.Forbidden(ForbiddenCode, "You do not have permission to perform this action.");

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
