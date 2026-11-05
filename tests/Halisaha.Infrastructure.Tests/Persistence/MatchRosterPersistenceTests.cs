using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using Halisaha.Infrastructure.Persistence;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>
/// Match aggregate'inin kadrosunun (MatchParticipation) veritabanına doğru yazıldığını kanıtlar.
/// Her adım yeni bir context'te yapılır; okunan her şey gerçekten PostgreSQL'den gelir.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MatchRosterPersistenceTests(PostgresFixture fixture)
{
    private readonly GraphBuilder _graph = new(fixture);

    // Maç kadrosuyla birlikte yüklenmeli: Include olmadan Participations boş gelir ve domain (IsParticipant,
    // kapasite) yanlış karar verir. Repository diliminde bu, "maç her zaman kadrosuyla yüklenir" sözleşmesi olacak.
    private static Task<Match> LoadMatchWithRosterAsync(HalisahaDbContext context, Guid matchId) =>
        GraphBuilder.LoadMatchWithRosterAsync(context, matchId);

    [Fact]
    public async Task Create_PersistsOrganizerParticipation()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var venueId = await _graph.CreateVenueAsync(districtId);
        var organizerId = await _graph.CreatePlayerAsync(districtId);

        var matchId = await _graph.CreateOpenMatchAsync(organizerId, venueId);

        await using var context = fixture.CreateContext();
        var match = await LoadMatchWithRosterAsync(context, matchId);
        var organizer = Assert.Single(match.Participations);
        Assert.Equal(organizerId, organizer.PlayerId);
        Assert.Equal(matchId, organizer.MatchId);
        Assert.Equal(Attendance.NotRecorded, organizer.Attendance);
        Assert.Null(organizer.Team);
    }

    // En kritik persistence senaryosu: yüklenmiş aggregate'e domain metoduyla eklenen yeni katılımın INSERT
    // edilmesi. MatchParticipationConfiguration'daki ValueGeneratedNever kaldırılınca (EF Core 10.0.14'te
    // denendi) EF, dolu Guid key'i "zaten var olan kayıt" sayıp katılımı Modified işaretliyor, olmayan satıra
    // UPDATE deniyor ve SaveChanges DbUpdateConcurrencyException fırlatıyor; bu test o durumda kırılıyor.
    // Bu hatayı Migrate/snapshot kontrolü yakalamıyor (şemada fark yok), yalnızca davranış testi yakalıyor.
    [Fact]
    public async Task ApplyThenAccept_PersistsNewParticipationAndAcceptedRequest()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var venueId = await _graph.CreateVenueAsync(districtId);
        var organizerId = await _graph.CreatePlayerAsync(districtId);
        var mertId = await _graph.CreatePlayerAsync(districtId);
        var matchId = await _graph.CreateOpenMatchAsync(organizerId, venueId);

        // Mert başvurur. Geçmiş, Application katmanının yapacağı gibi gerçekten DB'den okunur.
        Guid requestId;
        await using (var context = fixture.CreateContext())
        {
            var match = await LoadMatchWithRosterAsync(context, matchId);
            var pairHistory = await context.MatchRequests
                .Where(r => r.MatchId == matchId && r.PlayerId == mertId)
                .ToListAsync();

            var request = RequestPolicy.Apply(match, mertId, pairHistory, TestClock.Now);
            context.MatchRequests.Add(request);
            await context.SaveChangesAsync();
            requestId = request.Id;
        }

        // Organizatör kabul eder: yüklenmiş maça yeni katılım eklenir, istek Accepted olur.
        await using (var context = fixture.CreateContext())
        {
            var match = await LoadMatchWithRosterAsync(context, matchId);
            var request = await context.MatchRequests.SingleAsync(r => r.Id == requestId);

            RequestPolicy.Accept(match, request, organizerId, TestClock.Now);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            var match = await LoadMatchWithRosterAsync(context, matchId);
            Assert.Equal(2, match.Participations.Count);
            Assert.True(match.IsParticipant(organizerId));
            Assert.True(match.IsParticipant(mertId));

            var request = await context.MatchRequests.SingleAsync(r => r.Id == requestId);
            Assert.Equal(RequestStatus.Accepted, request.Status);
            Assert.Equal(TestClock.Now, request.ClosedAt);
        }
    }
}
