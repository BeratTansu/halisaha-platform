using Halisaha.Domain.Common;
using Halisaha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>
/// Eşleme tablosunun birim testleri, veritabanı yok: PostgresException'ın public constructor'ı SQLSTATE ve
/// constraint adını alır. Gerçek index adlarının DB'deki adla aynı olduğunu index testleri (DbAssert) ve
/// UnitOfWorkTests'teki gerçek EmailIndex ihlali kanıtlar; ikisi birlikte uçtan uca kapsamı verir.
/// </summary>
public class PostgresErrorTranslatorTests
{
    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "IX_MatchRequests_MatchId_PlayerId_Pending", "request.pending_exists")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "IX_Ratings_MatchId_RaterId_RateeId", "rating.already_rated")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "EmailIndex", "auth.email_taken")]
    [InlineData(PostgresErrorCodes.DeadlockDetected, null, "system.concurrency_conflict")]
    [InlineData(PostgresErrorCodes.SerializationFailure, null, "system.concurrency_conflict")]
    public void KnownError_IsTranslatedToConflict(string sqlState, string? constraintName, string expectedCode)
    {
        // SaveChanges'teki biçim: DbUpdateException içinde sarılı.
        var exception = new DbUpdateException("save failed", Postgres(sqlState, constraintName));

        Assert.True(PostgresErrorTranslator.TryTranslate(exception, out var translated));
        Assert.Equal(expectedCode, translated.Code);
        Assert.Equal(DomainErrorKind.Conflict, translated.Kind);
    }

    [Fact]
    public void UnwrappedPostgresException_IsTranslated()
    {
        // Ham SQL'deki biçim (kilit sorgusu): sarılmamış.
        var exception = Postgres(PostgresErrorCodes.DeadlockDetected, constraintName: null);

        Assert.True(PostgresErrorTranslator.TryTranslate(exception, out var translated));
        Assert.Equal("system.concurrency_conflict", translated.Code);
    }

    [Fact]
    public void DeeplyWrappedPostgresException_IsFound()
    {
        // Zincir tek seviye değil sonuna kadar aranır.
        var exception = new InvalidOperationException("outer",
            new DbUpdateException("save failed", Postgres(PostgresErrorCodes.UniqueViolation, "EmailIndex")));

        Assert.True(PostgresErrorTranslator.TryTranslate(exception, out var translated));
        Assert.Equal("auth.email_taken", translated.Code);
    }

    [Theory]
    // Kilit protokolü varken tetiklenmesi bir hata; görünür kalmalı.
    [InlineData(PostgresErrorCodes.UniqueViolation, "IX_MatchParticipations_MatchId_PlayerId")]
    // Tabloda olmayan bir unique index (Identity'nin kendi index'i).
    [InlineData(PostgresErrorCodes.UniqueViolation, "UserNameIndex")]
    // Adı gelmeyen unique ihlali: hangi kural olduğunu bilmeden çevirmek yanlış kod üretir.
    [InlineData(PostgresErrorCodes.UniqueViolation, null)]
    // FK ihlali: ilçe gibi referanslar önceden kontrol edilir; oluşursa bizim hatamız.
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "FK_PlayerProfiles_Districts_PreferredDistrictId")]
    // Tablodaki bir index adı, ama yanlış SQLSTATE ile: ad tek başına yetmez.
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "EmailIndex")]
    public void UndecidedError_IsNotTranslated(string sqlState, string? constraintName)
    {
        var exception = new DbUpdateException("save failed", Postgres(sqlState, constraintName));

        Assert.False(PostgresErrorTranslator.TryTranslate(exception, out var translated));
        Assert.Null(translated);
    }

    [Fact]
    public void NonPostgresException_IsNotTranslated()
    {
        // Handler'ın kendi DomainException'ı da bu yoldan geçer: zincirinde PostgresException yok.
        var exception = new DbUpdateConcurrencyException("0 rows affected");

        Assert.False(PostgresErrorTranslator.TryTranslate(exception, out _));
    }

    [Fact]
    public void EachTranslation_CreatesNewExceptionInstance()
    {
        // Paylaşılan örnek fırlatılırsa stack trace'ler birbirinin üzerine yazılır.
        var exception = Postgres(PostgresErrorCodes.UniqueViolation, "EmailIndex");

        PostgresErrorTranslator.TryTranslate(exception, out var first);
        PostgresErrorTranslator.TryTranslate(exception, out var second);

        Assert.NotSame(first, second);
    }

    private static PostgresException Postgres(string sqlState, string? constraintName) =>
        new("test", "ERROR", "ERROR", sqlState, constraintName: constraintName);
}
