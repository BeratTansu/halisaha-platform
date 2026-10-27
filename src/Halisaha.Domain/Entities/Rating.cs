namespace Halisaha.Domain.Entities;

/// <summary>A playing-score rating. Immutable once given. Created only through <see cref="Rules.RatingPolicy"/>.</summary>
public sealed class Rating
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid RaterId { get; private set; }
    public Guid RateeId { get; private set; }
    public int Score { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Rating() { }

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
