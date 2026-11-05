using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>Veritabanı kısıt ihlallerini ham haliyle doğrular (PostgresErrorTranslator henüz yok).</summary>
public static class DbAssert
{
    /// <summary>
    /// SaveChanges'in unique ihlaliyle reddedildiğini ve ihlal edilen index'in adını doğrular. Ad, testte bilerek
    /// string olarak yazılır: config'teki ad değişirse test kırılmalı, çünkü çeviri sözlüğü bu ada bağlı.
    /// </summary>
    public static async Task<PostgresException> ThrowsUniqueViolationAsync(Func<Task> action, string constraintName)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState); // 23505
        Assert.Equal(constraintName, postgres.ConstraintName);
        return postgres;
    }
}
