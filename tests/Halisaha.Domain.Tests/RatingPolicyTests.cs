using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using static Halisaha.Domain.Tests.TestData;

namespace Halisaha.Domain.Tests;

public class RatingPolicyTests
{
    private static (Halisaha.Domain.Entities.Match match, Guid a, Guid b) PresentPair()
    {
        Guid a = NewPlayer(), b = NewPlayer();
        var match = CompletedMatch(StartsAt, a, b);
        match.RecordAttendance(Organizer, a, Attendance.Present, StartsAt);
        match.RecordAttendance(Organizer, b, Attendance.Present, StartsAt);
        return (match, a, b);
    }

    [Fact]
    public void Present_players_can_rate_each_other()
    {
        var (match, a, b) = PresentPair();
        var rating = RatingPolicy.Rate(match, a, b, 4, alreadyRated: false, StartsAt);
        Assert.Equal(4, rating.Score);
    }

    [Fact]
    public void Self_rating_is_rejected()
    {
        var (match, a, _) = PresentPair();
        var ex = Assert.Throws<DomainException>(() => RatingPolicy.Rate(match, a, a, 5, false, StartsAt));
        Assert.Equal("rating.self", ex.Code);
    }

    [Fact]
    public void Absent_player_cannot_be_rated()
    {
        var (match, a, b) = PresentPair();
        match.RecordAttendance(Organizer, b, Attendance.Absent, StartsAt);
        var ex = Assert.Throws<DomainException>(() => RatingPolicy.Rate(match, a, b, 3, false, StartsAt));
        Assert.Equal("rating.ratee_not_present", ex.Code);
    }

    [Fact]
    public void Organizer_not_recorded_cannot_rate()
    {
        var (match, _, b) = PresentPair();
        var ex = Assert.Throws<DomainException>(() => RatingPolicy.Rate(match, Organizer, b, 3, false, StartsAt));
        Assert.Equal("rating.rater_not_present", ex.Code);
    }

    [Fact]
    public void Second_rating_for_same_person_is_rejected()
    {
        var (match, a, b) = PresentPair();
        var ex = Assert.Throws<DomainException>(() => RatingPolicy.Rate(match, a, b, 3, alreadyRated: true, StartsAt));
        Assert.Equal("rating.duplicate", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Score_out_of_range_is_rejected(int score)
    {
        var (match, a, b) = PresentPair();
        var ex = Assert.Throws<DomainException>(() => RatingPolicy.Rate(match, a, b, score, false, StartsAt));
        Assert.Equal("rating.invalid_score", ex.Code);
    }

    [Fact]
    public void Rating_closes_after_day_7()
    {
        var (match, a, b) = PresentPair();
        RatingPolicy.Rate(match, a, b, 4, false, StartsAt + MatchRules.RatingWindow);
        var ex = Assert.Throws<DomainException>(() =>
            RatingPolicy.Rate(match, b, a, 4, false, StartsAt + MatchRules.RatingWindow + TimeSpan.FromSeconds(1)));
        Assert.Equal("rating.closed", ex.Code);
    }
}
