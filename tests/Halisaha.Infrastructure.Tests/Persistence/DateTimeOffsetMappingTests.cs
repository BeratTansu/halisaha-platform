using Halisaha.Domain.Entities;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>
/// DateTimeOffset ↔ timestamptz eşlemesinin iki sınırı. Bu testler bizim kodumuzu değil Npgsql/PostgreSQL'in
/// davranışını belgeler; kütüphane güncellemesinde kırılırlarsa domain'e giden zamanların anlamı değişmiş demektir.
/// </summary>
[Collection(PostgresCollection.Name)]
public class DateTimeOffsetMappingTests(PostgresFixture fixture)
{
    private readonly GraphBuilder _graph = new(fixture);

    // .NET 100 ns (1 tick), PostgreSQL 1 µs (10 tick) hassasiyetinde saklar; fazlası yuvarlanmaz, kesilir.
    // TimeProvider.GetUtcNow() tick hassasiyetinde değer üretir: kaydedilip geri okunan zaman yazılanla eşit olmayabilir.
    [Fact]
    public async Task StartsAt_WithSubMicrosecondTicks_IsTruncatedToMicrosecondsAndStaysUtc()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var venueId = await _graph.CreateVenueAsync(districtId);
        var organizerId = await _graph.CreatePlayerAsync(districtId);
        var precise = TestClock.StartsAt.AddTicks(1_234_567); // 0,1234567 sn

        Guid matchId;
        await using (var context = fixture.CreateContext())
        {
            var match = Match.Create(organizerId, venueId, MatchType.Mixed, precise, 10, 0, null, TestClock.Now);
            context.Matches.Add(match);
            await context.SaveChangesAsync();
            matchId = match.Id;
        }

        await using (var context = fixture.CreateContext())
        {
            var stored = (await context.Matches.SingleAsync(m => m.Id == matchId)).StartsAt;

            Assert.NotEqual(precise, stored);
            Assert.Equal(precise.AddTicks(-(precise.Ticks % 10)), stored); // 0,1234560 sn
            Assert.Equal(TimeSpan.Zero, stored.Offset);
        }
    }

    // Npgsql timestamptz'ye yalnızca offset 0 yazar. Hata PostgresException değil ArgumentException'dır:
    // PostgresErrorTranslator bunu tanımaz (500). Domain'e yalnızca UTC zaman (TimeProvider.GetUtcNow) girmeli.
    [Fact]
    public async Task NonUtcOffset_IsRejectedByNpgsqlBeforeReachingDatabase()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var venueId = await _graph.CreateVenueAsync(districtId);
        var organizerId = await _graph.CreatePlayerAsync(districtId);
        var istanbulTime = new DateTimeOffset(2026, 11, 3, 21, 0, 0, TimeSpan.FromHours(3));

        await using var context = fixture.CreateContext();
        context.Matches.Add(Match.Create(organizerId, venueId, MatchType.Mixed, istanbulTime, 10, 0, null, TestClock.Now));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.IsType<ArgumentException>(exception.InnerException);
    }
}
