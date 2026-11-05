using Halisaha.Infrastructure.Identity;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>
/// Identity'nin EmailIndex'inin unique override'ı (ApplicationUserConfiguration) gerçek DB'de geçerli mi.
/// NormalizedEmail burada elle verilir: büyük-küçük harf normalizasyonu UserManager'ın işi ve Register diliminde test edilecek.
/// </summary>
[Collection(PostgresCollection.Name)]
public class IdentityIndexTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SecondUserWithSameNormalizedEmail_IsRejectedByEmailIndex()
    {
        var normalizedEmail = $"{Guid.NewGuid():N}@TEST.LOCAL";

        await using (var context = fixture.CreateContext())
        {
            context.Users.Add(NewUser(normalizedEmail));
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            context.Users.Add(NewUser(normalizedEmail));

            await DbAssert.ThrowsUniqueViolationAsync(() => context.SaveChangesAsync(), "EmailIndex");
        }
    }

    private static ApplicationUser NewUser(string normalizedEmail) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Kullanıcı",
        Email = normalizedEmail.ToLowerInvariant(),
        NormalizedEmail = normalizedEmail,
        CreatedAt = TestClock.Now,
    };
}
