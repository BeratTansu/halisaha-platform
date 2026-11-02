using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Errors;

namespace Halisaha.Domain.Rules;

/// <summary>
/// Who may rate whom, when, and in which order the checks run.
/// Error priority: Shape (400) → Who (403) → When (409) → What (409).
/// Rating never changes the match, so it needs no match lock; the unique index on
/// (MatchId, RaterId, RateeId) guards duplicate ratings under a race.
/// </summary>
public static class RatingPolicy
{
    /// <summary>
    /// The rater (the signed-in user) gives the ratee a score for this match.
    /// </summary>
    /// <param name="raterHistory">
    /// Every rating this rater has given in this match. A row for another match or rater is a
    /// programming error: <see cref="InvalidOperationException"/>.
    /// </param>
    public static Rating Rate(
        Match match, Guid raterId, Guid rateeId, int score,
        IReadOnlyCollection<Rating> raterHistory, DateTimeOffset now)
    {
        // Shape
        if (rateeId == Guid.Empty)
        {
            throw MatchErrors.PlayerRequired();
        }

        if (score is < MatchRules.MinScore or > MatchRules.MaxScore)
        {
            throw RatingErrors.InvalidScore();
        }

        if (raterId == rateeId)
        {
            throw RatingErrors.SelfRating();
        }

        // Who: only someone on this match's roster has any right to rate here.
        var rater = match.ParticipationOf(raterId) ?? throw RatingErrors.NotParticipant();

        EnsureRaterHistory(match, raterId, raterHistory);

        // When
        if (match.Status != MatchStatus.Completed)
        {
            throw RatingErrors.MatchNotCompleted();
        }

        if (!match.IsRatingOpen(now))
        {
            throw RatingErrors.WindowClosed();
        }

        // What: attendance is the current state of the roster, so it can still change within its window.
        if (rater.Attendance != Attendance.Present)
        {
            throw RatingErrors.RaterNotPresent();
        }

        if (match.ParticipationOf(rateeId)?.Attendance != Attendance.Present)
        {
            throw RatingErrors.RateeNotPresent();
        }

        if (raterHistory.Any(rating => rating.RateeId == rateeId))
        {
            throw RatingErrors.AlreadyRated();
        }

        return new Rating(match.Id, raterId, rateeId, score, now);
    }

    private static void EnsureRaterHistory(Match match, Guid raterId, IReadOnlyCollection<Rating> raterHistory)
    {
        if (raterHistory.Any(rating => rating.MatchId != match.Id || rating.RaterId != raterId))
        {
            throw new InvalidOperationException($"Rater history for match {match.Id} and rater {raterId} contains another pair's rating.");
        }
    }
}
