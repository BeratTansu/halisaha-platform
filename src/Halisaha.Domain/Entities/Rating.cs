namespace Halisaha.Domain.Entities;

/// <summary>
/// One player's score for another after a completed match. Immutable: there is no method that changes it.
/// Created only through <see cref="Rules.RatingPolicy"/>. Never deleted; statistics count it only while
/// both sides are still marked Present, so a corrected attendance fixes averages without losing data.
/// </summary>
public class Rating
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid RaterId { get; private set; }
    public Guid RateeId { get; private set; }
    public int Score { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Rating() { } // EF Core

    internal Rating(Guid matchId, Guid raterId, Guid rateeId, int score, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        MatchId = matchId;
        RaterId = raterId;
        RateeId = rateeId;
        Score = score;
        CreatedAt = now;
    }
}
