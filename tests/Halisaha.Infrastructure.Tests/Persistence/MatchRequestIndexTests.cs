using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using Halisaha.Infrastructure.Persistence;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>Bir (maç, oyuncu) çiftinde türden bağımsız tek bekleyen istek kuralının DB tarafı.</summary>
[Collection(PostgresCollection.Name)]
public class MatchRequestIndexTests(PostgresFixture fixture)
{
    private readonly GraphBuilder _graph = new(fixture);

    // Yarış: iki istek de diğerini görmeden geçer. Bunu boş pairHistory ile iki Apply olarak kuruyoruz;
    // domain kontrolü atlanmış olur ve DB'ye iki Pending gider. İkincisini partial unique index reddetmeli.
    [Fact]
    public async Task SecondPendingRequestForSamePair_IsRejectedByPendingIndex()
    {
        var scenario = await _graph.CreateOpenMatchWithOutsiderAsync();

        await using (var context = fixture.CreateContext())
        {
            var match = await GraphBuilder.LoadMatchWithRosterAsync(context, scenario.MatchId);
            context.MatchRequests.Add(RequestPolicy.Apply(match, scenario.PlayerId, [], TestClock.Now));
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            var match = await GraphBuilder.LoadMatchWithRosterAsync(context, scenario.MatchId);
            context.MatchRequests.Add(RequestPolicy.Apply(match, scenario.PlayerId, [], TestClock.Now));

            await DbAssert.ThrowsUniqueViolationAsync(
                () => context.SaveChangesAsync(), "IX_MatchRequests_MatchId_PlayerId_Pending");
        }
    }

    // Filtre ("Status" = 1) kapanmış istekleri index'in dışında bırakmalı; tam unique olsaydı geri çekilen
    // başvurudan sonra yeniden başvurmak (meşru bir akış) DB'de reddedilirdi.
    [Fact]
    public async Task NewPendingRequestAfterWithdrawal_IsAllowed()
    {
        var scenario = await _graph.CreateOpenMatchWithOutsiderAsync();

        Guid firstRequestId;
        await using (var context = fixture.CreateContext())
        {
            var match = await GraphBuilder.LoadMatchWithRosterAsync(context, scenario.MatchId);
            var request = RequestPolicy.Apply(match, scenario.PlayerId, [], TestClock.Now);
            context.MatchRequests.Add(request);
            await context.SaveChangesAsync();
            firstRequestId = request.Id;
        }

        await using (var context = fixture.CreateContext())
        {
            var match = await GraphBuilder.LoadMatchWithRosterAsync(context, scenario.MatchId);
            var request = await context.MatchRequests.SingleAsync(r => r.Id == firstRequestId);
            RequestPolicy.Withdraw(match, request, scenario.PlayerId, TestClock.Now);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            var match = await GraphBuilder.LoadMatchWithRosterAsync(context, scenario.MatchId);
            var pairHistory = await PairHistoryAsync(context, scenario);
            context.MatchRequests.Add(RequestPolicy.Apply(match, scenario.PlayerId, pairHistory, TestClock.Now));
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            var statuses = (await PairHistoryAsync(context, scenario)).Select(r => r.Status).Order().ToList();
            Assert.Equal([RequestStatus.Pending, RequestStatus.Withdrawn], statuses);
        }
    }

    private static Task<List<MatchRequest>> PairHistoryAsync(HalisahaDbContext context, GraphBuilder.MatchScenario scenario) =>
        context.MatchRequests
            .Where(r => r.MatchId == scenario.MatchId && r.PlayerId == scenario.PlayerId)
            .ToListAsync();
}
