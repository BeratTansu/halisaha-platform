using Halisaha.Application.Abstractions.Identity;
using Halisaha.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>Identity testlerinin ortak verisi ve "DB'de ne var" sorguları (her zaman ayrı bir context'ten).</summary>
public static class TestUsers
{
    /// <summary>C3 politikasını karşılayan parola (8+ karakter, rakam, küçük harf).</summary>
    public const string ValidPassword = "halisaha1";

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    public static NewIdentityUser New(string? email = null, Guid? userId = null) => new(
        userId ?? Guid.NewGuid(),
        email ?? UniqueEmail(),
        "Ahmet",
        "Yılmaz",
        "+90 555 000 00 00",
        TestClock.Now);

    public static async Task<bool> UserExistsAsync(this PostgresFixture fixture, Guid userId)
    {
        await using var context = fixture.CreateContext();
        return await context.Users.AnyAsync(u => u.Id == userId);
    }

    public static async Task<bool> ProfileExistsAsync(this PostgresFixture fixture, Guid userId)
    {
        await using var context = fixture.CreateContext();
        return await context.PlayerProfiles.AnyAsync(p => p.UserId == userId);
    }

    public static async Task<int> CountUsersWithNormalizedEmailAsync(this PostgresFixture fixture, string email)
    {
        await using var context = fixture.CreateContext();
        var normalized = email.ToUpperInvariant();
        return await context.Users.CountAsync(u => u.NormalizedEmail == normalized);
    }
}
