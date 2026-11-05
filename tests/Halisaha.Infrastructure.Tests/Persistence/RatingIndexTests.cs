using Halisaha.Domain.Rules;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>Aynı maçta aynı kişiye ikinci puanın DB tarafında reddedilmesi.</summary>
[Collection(PostgresCollection.Name)]
public class RatingIndexTests(PostgresFixture fixture)
{
    private readonly GraphBuilder _graph = new(fixture);

    // Yarış (ör. çift tıklama): iki istek de raterHistory'yi boş görür. Boş geçmişle iki Rate bunu kurar;
    // domain kontrolü atlanır ve ikinci kaydı unique index reddetmeli.
    [Fact]
    public async Task SecondRatingForSameTriple_IsRejectedByRatingIndex()
    {
        var scenario = await _graph.CreateCompletedMatchWithPresentPlayerAsync();

        await using (var context = fixture.CreateContext())
        {
            var match = await GraphBuilder.LoadMatchWithRosterAsync(context, scenario.MatchId);
            context.Ratings.Add(RatingPolicy.Rate(match, scenario.OrganizerId, scenario.PlayerId, 4, [], TestClock.CompletedAt));
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            var match = await GraphBuilder.LoadMatchWithRosterAsync(context, scenario.MatchId);
            context.Ratings.Add(RatingPolicy.Rate(match, scenario.OrganizerId, scenario.PlayerId, 5, [], TestClock.CompletedAt));

            await DbAssert.ThrowsUniqueViolationAsync(
                () => context.SaveChangesAsync(), "IX_Ratings_MatchId_RaterId_RateeId");
        }
    }
}
