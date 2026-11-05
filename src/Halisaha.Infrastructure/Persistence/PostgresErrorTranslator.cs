using System.Diagnostics.CodeAnalysis;
using Halisaha.Application.Errors;
using Halisaha.Domain.Common;
using Halisaha.Domain.Errors;
using Npgsql;

namespace Halisaha.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL hatalarını kararlaştırılmış hatalara çeviren tek yer (Karar 10 eşleme tablosu). Saf fonksiyon:
/// veritabanına, DbContext'e veya log'a dokunmaz, yalnızca exception'a bakar. Tabloda olmayan her şey çevrilmez
/// ve 500 olarak görünür kalır.
/// </summary>
internal static class PostgresErrorTranslator
{
    // Index adı -> hata. Adlar configuration'lardaki index adlarıyla aynı olmalı. Ortak bir sabit yerine bilerek
    // string: index testleri (DbAssert) de adı string olarak doğruluyor; config'teki ad değişirse o testler kırılır
    // ve bu sözlüğün de güncellenmesi gerektiği görünür. Factory'ler her çağrıda yeni exception üretir;
    // exception örneği paylaşılmaz (stack trace fırlatıldığı anda örneğe yazılır).
    // Bilerek yok: IX_MatchParticipations_MatchId_PlayerId. Kilit protokolü varken tetiklenmesi bir hatadır.
    private static readonly Dictionary<string, Func<DomainException>> UniqueViolations = new(StringComparer.Ordinal)
    {
        ["IX_MatchRequests_MatchId_PlayerId_Pending"] = RequestErrors.PendingExists,
        ["IX_Ratings_MatchId_RaterId_RateeId"] = RatingErrors.AlreadyRated,
        ["EmailIndex"] = AuthErrors.EmailTaken,
    };

    /// <summary>
    /// Exception zincirinde (kendisi ve InnerException'ları) bir <see cref="PostgresException"/> arar; tabloda
    /// varsa çevirir. Zincir aranır, çünkü aynı hata iki biçimde gelebilir: SaveChanges'te
    /// <c>DbUpdateException</c> içinde sarılı, ham SQL'de (kilit sorgusu) sarılmamış.
    /// </summary>
    public static bool TryTranslate(Exception exception, [NotNullWhen(true)] out DomainException? translated)
    {
        translated = FindPostgresException(exception) is { } postgres ? Translate(postgres) : null;
        return translated is not null;
    }

    private static DomainException? Translate(PostgresException postgres) => postgres.SqlState switch
    {
        // Deadlock (40P01) veya serileştirme hatası (40001): istek doğru, zamanlama çakıştı.
        PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure =>
            SystemErrors.ConcurrencyConflict(),

        // 23505 tek başına yetmez: hangi kuralın ihlal edildiğini constraint adı söyler.
        PostgresErrorCodes.UniqueViolation
            when postgres.ConstraintName is { } name && UniqueViolations.TryGetValue(name, out var create) =>
            create(),

        // 23503 (FK) dahil geri kalan her şey bizim hatamız ya da beklenmeyen bir durum: çevrilmez.
        _ => null,
    };

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }
}
