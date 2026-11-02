using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;

namespace Halisaha.Domain.Tests;

public class RatingPolicyTests
{
    private static readonly Guid Organizer = TestData.Organizer;
    private static readonly Guid Mert = TestData.Mert;
    private static readonly Guid Emre = TestData.Emre;
    private static readonly DateTimeOffset RatedAt = TestData.CompletedAt.AddHours(1);
    private static readonly DateTimeOffset RatingDeadline = TestData.CompletedAt + MatchRules.RatingWindow;
    private static readonly Rating[] NoHistory = [];

    /// <summary>Completed match where the organizer (automatically), Mert and Emre are all Present.</summary>
    private static Match PlayedMatch()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(Mert, TestData.Now);
        match.AddParticipant(Emre, TestData.Now);
        match.Complete(Organizer, TestData.CompletedAt);
        match.RecordAttendance(Organizer, Mert, Attendance.Present, TestData.CompletedAt);
        match.RecordAttendance(Organizer, Emre, Attendance.Present, TestData.CompletedAt);
        return match;
    }

    private static DomainException Fails(Action action, string code)
    {
        var ex = Assert.Throws<DomainException>(action);
        Assert.Equal(code, ex.Code);
        return ex;
    }

    // ---- Success ----

    [Fact]
    public void Rate_BetweenPresentPlayers_CreatesRating()
    {
        var match = PlayedMatch();

        var rating = RatingPolicy.Rate(match, Mert, Emre, 4, NoHistory, RatedAt);

        Assert.Equal(match.Id, rating.MatchId);
        Assert.Equal(Mert, rating.RaterId);
        Assert.Equal(Emre, rating.RateeId);
        Assert.Equal(4, rating.Score);
        Assert.Equal(RatedAt, rating.CreatedAt);
    }

    // The organizer is Present by completing the match, so he rates and is rated like anyone else.
    [Fact]
    public void Rate_OrganizerAndPlayer_BothDirectionsAllowed()
    {
        var match = PlayedMatch();

        RatingPolicy.Rate(match, Organizer, Mert, 5, NoHistory, RatedAt);
        var rating = RatingPolicy.Rate(match, Mert, Organizer, 3, NoHistory, RatedAt);

        Assert.Equal(Organizer, rating.RateeId);
    }

    [Theory]
    [InlineData(MatchRules.MinScore)]
    [InlineData(MatchRules.MaxScore)]
    public void Rate_AtScoreBounds_IsAllowed(int score)
        => Assert.Equal(score, RatingPolicy.Rate(PlayedMatch(), Mert, Emre, score, NoHistory, RatedAt).Score);

    // Both ends of the 7-day window are inclusive.
    [Fact]
    public void Rate_AtCompletionAndAtDeadline_IsAllowed()
    {
        var match = PlayedMatch();

        RatingPolicy.Rate(match, Mert, Emre, 4, NoHistory, TestData.CompletedAt);
        RatingPolicy.Rate(match, Emre, Mert, 4, NoHistory, RatingDeadline);
    }

    // Mert already rated Emre; rating the organizer is a different pair.
    [Fact]
    public void Rate_AnotherRateeAfterRatingOne_IsAllowed()
    {
        var match = PlayedMatch();
        var first = RatingPolicy.Rate(match, Mert, Emre, 4, NoHistory, RatedAt);

        var second = RatingPolicy.Rate(match, Mert, Organizer, 5, [first], RatedAt);

        Assert.Equal(Organizer, second.RateeId);
    }

    // ---- Shape ----

    [Fact]
    public void Rate_WithEmptyRatee_ThrowsPlayerRequired()
    {
        var ex = Fails(() => RatingPolicy.Rate(PlayedMatch(), Mert, Guid.Empty, 4, NoHistory, RatedAt), "match.player_required");
        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
    }

    [Theory]
    [InlineData(MatchRules.MinScore - 1)]
    [InlineData(MatchRules.MaxScore + 1)]
    public void Rate_OutOfRangeScore_ThrowsInvalidScore(int score)
    {
        var ex = Fails(() => RatingPolicy.Rate(PlayedMatch(), Mert, Emre, score, NoHistory, RatedAt), "rating.invalid_score");
        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
    }

    [Fact]
    public void Rate_Self_ThrowsSelfRating()
    {
        var ex = Fails(() => RatingPolicy.Rate(PlayedMatch(), Mert, Mert, 5, NoHistory, RatedAt), "rating.self_rating");
        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
    }

    // Shape comes before Who: a stranger with a bad score hears about the score.
    [Fact]
    public void Rate_ByStrangerWithInvalidScore_ThrowsInvalidScore()
    {
        var stranger = Guid.NewGuid();

        Fails(() => RatingPolicy.Rate(PlayedMatch(), stranger, Mert, 0, NoHistory, RatedAt), "rating.invalid_score");
    }

    // ---- Who ----

    [Fact]
    public void Rate_ByStranger_ThrowsNotParticipant()
    {
        var ex = Fails(() => RatingPolicy.Rate(PlayedMatch(), Guid.NewGuid(), Mert, 4, NoHistory, RatedAt), "rating.not_participant");
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }

    // Who comes before When: a stranger learns nothing about whether the match was completed.
    [Fact]
    public void Rate_ByStrangerOnOpenMatch_ThrowsNotParticipant()
    {
        Fails(() => RatingPolicy.Rate(TestData.OpenMatch(), Guid.NewGuid(), Organizer, 4, NoHistory, TestData.Now), "rating.not_participant");
    }

    // ---- When ----

    [Fact]
    public void Rate_OnOpenMatch_ThrowsMatchNotCompleted()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(Mert, TestData.Now);

        Fails(() => RatingPolicy.Rate(match, Mert, Organizer, 4, NoHistory, TestData.StartsAt.AddHours(1)), "rating.match_not_completed");
    }

    [Fact]
    public void Rate_OnCancelledMatch_ThrowsMatchNotCompleted()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(Mert, TestData.Now);
        match.Cancel(Organizer, TestData.Now);

        Fails(() => RatingPolicy.Rate(match, Mert, Organizer, 4, NoHistory, TestData.Now), "rating.match_not_completed");
    }

    [Fact]
    public void Rate_AfterDeadline_ThrowsWindowClosed()
    {
        Fails(() => RatingPolicy.Rate(PlayedMatch(), Mert, Emre, 4, NoHistory, RatingDeadline + TestData.Tick), "rating.window_closed");
    }

    // ---- What ----

    // Mert was not marked yet: he cannot rate until the organizer records him as Present.
    [Fact]
    public void Rate_ByNotRecordedPlayer_ThrowsRaterNotPresent()
    {
        var match = TestData.CompletedMatch(); // Mert joined but his attendance is NotRecorded

        Fails(() => RatingPolicy.Rate(match, Mert, Organizer, 4, NoHistory, RatedAt), "rating.rater_not_present");
    }

    [Fact]
    public void Rate_ByAbsentPlayer_ThrowsRaterNotPresent()
    {
        var match = PlayedMatch();
        match.RecordAttendance(Organizer, Mert, Attendance.Absent, TestData.CompletedAt);

        Fails(() => RatingPolicy.Rate(match, Mert, Emre, 4, NoHistory, RatedAt), "rating.rater_not_present");
    }

    [Fact]
    public void Rate_AbsentPlayer_ThrowsRateeNotPresent()
    {
        var match = PlayedMatch();
        match.RecordAttendance(Organizer, Emre, Attendance.Absent, TestData.CompletedAt);

        Fails(() => RatingPolicy.Rate(match, Mert, Emre, 4, NoHistory, RatedAt), "rating.ratee_not_present");
    }

    // Someone who was never in the match cannot be rated either.
    [Fact]
    public void Rate_NonParticipant_ThrowsRateeNotPresent()
    {
        Fails(() => RatingPolicy.Rate(PlayedMatch(), Mert, Guid.NewGuid(), 4, NoHistory, RatedAt), "rating.ratee_not_present");
    }

    [Fact]
    public void Rate_SamePairTwice_ThrowsAlreadyRated()
    {
        var match = PlayedMatch();
        var first = RatingPolicy.Rate(match, Mert, Emre, 4, NoHistory, RatedAt);

        Fails(() => RatingPolicy.Rate(match, Mert, Emre, 2, [first], RatedAt), "rating.already_rated");
    }

    // Emre rating Mert back is a different direction, not a duplicate.
    [Fact]
    public void Rate_ReverseDirection_IsAllowed()
    {
        var match = PlayedMatch();
        RatingPolicy.Rate(match, Mert, Emre, 4, NoHistory, RatedAt);

        var back = RatingPolicy.Rate(match, Emre, Mert, 3, NoHistory, RatedAt);

        Assert.Equal(Mert, back.RateeId);
    }

    // ---- Programming errors ----

    [Fact]
    public void Rate_WithAnotherRatersHistory_ThrowsInvalidOperation()
    {
        var match = PlayedMatch();
        var emresRating = RatingPolicy.Rate(match, Emre, Organizer, 4, NoHistory, RatedAt);

        Assert.Throws<InvalidOperationException>(() => RatingPolicy.Rate(match, Mert, Emre, 4, [emresRating], RatedAt));
    }
}
