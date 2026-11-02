using Halisaha.Domain.Common;
using Halisaha.Domain.Rules;

namespace Halisaha.Domain.Errors;

/// <summary>
/// Every error rating can raise. Infrastructure throws <see cref="AlreadyRated"/> too,
/// when the (MatchId, RaterId, RateeId) unique index rejects a race.
/// </summary>
public static class RatingErrors
{
    public static DomainException InvalidScore() =>
        DomainException.Validation("rating.invalid_score", $"Score must be between {MatchRules.MinScore} and {MatchRules.MaxScore}.");

    public static DomainException SelfRating() =>
        DomainException.Validation("rating.self_rating", "Players cannot rate themselves.");

    public static DomainException NotParticipant() =>
        DomainException.Forbidden("rating.not_participant", "Only players of this match can rate in it.");

    public static DomainException MatchNotCompleted() =>
        DomainException.Conflict("rating.match_not_completed", "Ratings open only after the match is completed.");

    public static DomainException WindowClosed() =>
        DomainException.Conflict("rating.window_closed", "The rating window for this match has closed.");

    public static DomainException RaterNotPresent() =>
        DomainException.Conflict("rating.rater_not_present", "Only players marked present can rate.");

    public static DomainException RateeNotPresent() =>
        DomainException.Conflict("rating.ratee_not_present", "Only players marked present can be rated.");

    public static DomainException AlreadyRated() =>
        DomainException.Conflict("rating.already_rated", "You have already rated this player for this match.");
}
