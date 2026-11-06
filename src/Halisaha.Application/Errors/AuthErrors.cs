using Halisaha.Domain.Common;

namespace Halisaha.Application.Errors;

/// <summary>
/// Kimlik (kayıt/giriş) hataları. Bir domain kuralı değil, ama Domain'deki hata sınıflarıyla aynı kalıbı izler:
/// tür, kod ve mesaj tek yerde. İki yoldan fırlatılır: Register handler'ı (Identity'nin DuplicateEmail sonucu)
/// ve PostgresErrorTranslator (EmailIndex yarışı, 23505).
/// </summary>
public static class AuthErrors
{
    public static DomainException EmailTaken() =>
        DomainException.Conflict("auth.email_taken", "An account with this email address already exists.");
}
