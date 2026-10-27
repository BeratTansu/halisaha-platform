using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Rules;

public static class RatingPolicy
{
    /// <summary>
    /// Only players marked Present give and receive ratings, never to themselves,
    /// once per person per match, within 7 days of completion.
    /// </summary>
    public static Rating Rate(Match match, Guid raterId, Guid rateeId, int score, bool alreadyRated, DateTimeOffset now)
    {
        if (!match.IsRatingOpen(now))
            throw DomainException.Conflict("rating.closed", "Ratings are open for 7 days after the match is completed.");
        if (raterId == rateeId)
            throw DomainException.Validation("rating.self", "You cannot rate yourself.");
        if (score is < MatchRules.MinScore or > MatchRules.MaxScore)
            throw DomainException.Validation("rating.invalid_score", "Score must be between 1 and 5.");
        if (match.ParticipationOf(raterId)?.Attendance != Attendance.Present)
            throw DomainException.Forbidden("rating.rater_not_present", "Only players marked present can give ratings.");
        if (match.ParticipationOf(rateeId)?.Attendance != Attendance.Present)
            throw DomainException.Conflict("rating.ratee_not_present", "Only players marked present can be rated.");
        if (alreadyRated)
            throw DomainException.Conflict("rating.duplicate", "You have already rated this player for this match.");

        return new Rating(match.Id, raterId, rateeId, score, now);
    }
}
