using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>
/// Silme zinciri: ApplicationUser → PlayerProfile Cascade, PlayerProfile → maç/katılım/istek/puan Restrict.
/// Sonuç: geçmişi olmayan kullanıcı silinebilir, geçmişi olanın silinmesi DB'de reddedilir.
/// Silme her zaman YALNIZCA kullanıcının takip edildiği temiz bir context'te yapılır: profil veya maç da takip
/// ediliyor olsaydı EF, DB'ye gitmeden kendi InvalidOperationException'ını fırlatırdı ve PostgreSQL'in kuralını
/// değil EF'in istemci tarafı kontrolünü test etmiş olurduk.
/// </summary>
[Collection(PostgresCollection.Name)]
public class DeleteBehaviorTests(PostgresFixture fixture)
{
    private readonly GraphBuilder _graph = new(fixture);

    [Fact]
    public async Task DeletingUserWithMatchHistory_IsRejectedByRestrict()
    {
        // Oyuncu kabul edilmiş, maç tamamlanmış: profiline katılım ve istek kayıtları bağlı.
        var scenario = await _graph.CreateCompletedMatchWithPresentPlayerAsync();

        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.SingleAsync(u => u.Id == scenario.PlayerId);
            context.Users.Remove(user);

            await DbAssert.ThrowsForeignKeyViolationAsync(() => context.SaveChangesAsync());
        }

        await using (var context = fixture.CreateContext())
        {
            Assert.True(await context.Users.AnyAsync(u => u.Id == scenario.PlayerId));
            Assert.True(await context.PlayerProfiles.AnyAsync(p => p.UserId == scenario.PlayerId));
        }
    }

    [Fact]
    public async Task DeletingUserWithoutHistory_CascadesToPlayerProfile()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var userId = await _graph.CreatePlayerAsync(districtId);

        await using (var context = fixture.CreateContext())
        {
            var user = await context.Users.SingleAsync(u => u.Id == userId);
            context.Users.Remove(user);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            Assert.False(await context.Users.AnyAsync(u => u.Id == userId));
            Assert.False(await context.PlayerProfiles.AnyAsync(p => p.UserId == userId));
        }
    }
}
