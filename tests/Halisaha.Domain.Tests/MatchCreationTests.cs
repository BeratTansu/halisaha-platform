using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using MatchType = Halisaha.Domain.Enums.MatchType;
using static Halisaha.Domain.Tests.TestData;

namespace Halisaha.Domain.Tests;

public class MatchCreationTests
{
    [Fact]
    public void Organizer_is_added_to_the_roster_automatically()
    {
        var match = OpenMatch();
        Assert.True(match.IsParticipant(Organizer));
        Assert.Single(match.Participations);
        Assert.Equal(MatchStatus.Open, match.Status);
    }

    [Fact]
    public void Missing_players_is_capacity_minus_external_minus_roster()
    {
        var match = OpenMatch(capacity: 14, external: 5);
        Assert.Equal(14 - 5 - 1, match.MissingPlayers);
    }

    [Fact]
    public void Mixed_match_with_odd_capacity_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => OpenMatch(MatchType.Mixed, capacity: 13));
        Assert.Equal("match.mixed_capacity_odd", ex.Code);
    }

    [Fact]
    public void Match_with_opponent_allows_odd_capacity()
    {
        var match = OpenMatch(MatchType.WithOpponent, capacity: 7);
        Assert.Equal(7, match.Capacity);
    }

    [Fact]
    public void External_players_plus_organizer_can_fill_capacity_exactly()
    {
        var match = OpenMatch(MatchType.WithOpponent, capacity: 7, external: 6);
        Assert.True(match.IsFull);
    }

    [Fact]
    public void External_players_plus_organizer_cannot_exceed_capacity()
    {
        var ex = Assert.Throws<DomainException>(() => OpenMatch(MatchType.WithOpponent, capacity: 7, external: 7));
        Assert.Equal("match.capacity_exceeded", ex.Code);
    }

    [Fact]
    public void Start_time_in_the_past_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Match.Create(Organizer, 1, MatchType.Mixed, Now.AddMinutes(-1), 10, 0, null, Now));
        Assert.Equal("match.start_in_past", ex.Code);
    }

    [Fact]
    public void Capacity_below_minimum_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(() => OpenMatch(MatchType.WithOpponent, capacity: 1));
        Assert.Equal("match.invalid_capacity", ex.Code);
    }
}
