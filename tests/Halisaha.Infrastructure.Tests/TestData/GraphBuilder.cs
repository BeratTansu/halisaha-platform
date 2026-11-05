using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using Halisaha.Infrastructure.Identity;
using Halisaha.Infrastructure.Persistence;
using Halisaha.Infrastructure.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>
/// Testlerin önkoşul verisini (il → ilçe → saha → kullanıcı + profil → maç) kurar. Her çağrı kendi kayıtlarını
/// oluşturur, testler birbirinin verisini paylaşmaz. Yalnızca domain'in public API'sini kullanır; her kayıt
/// kendi context'inde kaydedilir ki test, gerçekten DB'den okunan veriyle çalışsın.
/// </summary>
public sealed class GraphBuilder(PostgresFixture fixture)
{
    /// <summary>Bir senaryonun Id'leri: maç, organizatör ve kadroda olmayan (ya da sonradan eklenen) bir oyuncu.</summary>
    public sealed record MatchScenario(Guid MatchId, Guid OrganizerId, Guid PlayerId);

    public async Task<int> CreateDistrictAsync()
    {
        var provinceId = fixture.NextLocationId();
        var districtId = fixture.NextLocationId();

        await using var context = fixture.CreateContext();
        context.Provinces.Add(new Province(provinceId, $"Test İl {provinceId}"));
        context.Districts.Add(new District(districtId, provinceId, $"Test İlçe {districtId}"));
        await context.SaveChangesAsync();

        return districtId;
    }

    /// <summary>Venue Id'sini veritabanı üretir; kaydettikten sonra okunur.</summary>
    public async Task<int> CreateVenueAsync(int districtId)
    {
        var venue = new Venue("Test Saha", "Test Adres", 41.0, 29.0, districtId);

        await using var context = fixture.CreateContext();
        context.Venues.Add(venue);
        await context.SaveChangesAsync();

        return venue.Id;
    }

    /// <summary>
    /// Kullanıcı + oyuncu profili. UserManager kullanılmaz: bu katmanda şemayı test ediyoruz, Identity'nin
    /// normalizasyonu Register diliminin konusu. NormalizedEmail Id'den türetildiği için benzersiz.
    /// </summary>
    public async Task<Guid> CreatePlayerAsync(int districtId)
    {
        var userId = Guid.NewGuid();
        var email = $"{userId:N}@test.local";

        await using var context = fixture.CreateContext();
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            FirstName = "Test",
            LastName = "Oyuncu",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            CreatedAt = TestClock.Now,
        });
        context.PlayerProfiles.Add(new PlayerProfile(userId, Position.Midfielder, skillLevel: 3, districtId));
        await context.SaveChangesAsync();

        return userId;
    }

    /// <summary>Açık bir karma maç; Match.Create organizatörü kadroya kendisi ekler.</summary>
    public async Task<Guid> CreateOpenMatchAsync(Guid organizerId, int venueId)
    {
        var match = Match.Create(
            organizerId, venueId, MatchType.Mixed, TestClock.StartsAt,
            capacity: 10, externalPlayerCount: 0, description: null, TestClock.Now);

        await using var context = fixture.CreateContext();
        context.Matches.Add(match);
        await context.SaveChangesAsync();

        return match.Id;
    }

    /// <summary>Açık maç + kadroda olmayan bir oyuncu. Oyuncu ile organizatör aynı ilçede.</summary>
    public async Task<MatchScenario> CreateOpenMatchWithOutsiderAsync()
    {
        var districtId = await CreateDistrictAsync();
        var venueId = await CreateVenueAsync(districtId);
        var organizerId = await CreatePlayerAsync(districtId);
        var playerId = await CreatePlayerAsync(districtId);
        var matchId = await CreateOpenMatchAsync(organizerId, venueId);

        return new MatchScenario(matchId, organizerId, playerId);
    }

    /// <summary>
    /// Puanlamaya hazır maç: oyuncu başvurur ve kabul edilir, organizatör maçı tamamlar (kendi yoklaması Present olur)
    /// ve oyuncuyu Present işaretler. Her adım domain'in public akışıyla ve kendi context'inde yapılır.
    /// </summary>
    public async Task<MatchScenario> CreateCompletedMatchWithPresentPlayerAsync()
    {
        var scenario = await CreateOpenMatchWithOutsiderAsync();

        await using (var context = fixture.CreateContext())
        {
            var match = await LoadMatchWithRosterAsync(context, scenario.MatchId);
            var request = RequestPolicy.Apply(match, scenario.PlayerId, [], TestClock.Now);
            context.MatchRequests.Add(request);
            RequestPolicy.Accept(match, request, scenario.OrganizerId, TestClock.Now);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            var match = await LoadMatchWithRosterAsync(context, scenario.MatchId);
            match.Complete(scenario.OrganizerId, TestClock.CompletedAt);
            match.RecordAttendance(scenario.OrganizerId, scenario.PlayerId, Attendance.Present, TestClock.CompletedAt);
            await context.SaveChangesAsync();
        }

        return scenario;
    }

    /// <summary>Maç kadrosuyla birlikte yüklenmeli; Include olmadan Participations boş gelir.</summary>
    public static Task<Match> LoadMatchWithRosterAsync(HalisahaDbContext context, Guid matchId) =>
        context.Matches.Include(m => m.Participations).SingleAsync(m => m.Id == matchId);
}
