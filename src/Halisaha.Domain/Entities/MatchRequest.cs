using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Entities;

/// <summary>
/// One application (player → match) or invitation (organizer → player).
/// Created and changed only through <see cref="Rules.RequestPolicy"/>, which knows the match and the actor;
/// this class only guards its own state transitions.
/// </summary>
public class MatchRequest
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid PlayerId { get; private set; }
    public RequestType Type { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the request left Pending (Accepted, Rejected or Withdrawn). Status tells which.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    private MatchRequest() { } // EF Core

    internal MatchRequest(Guid matchId, Guid playerId, RequestType type, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        MatchId = matchId;
        PlayerId = playerId;
        Type = type;
        Status = RequestStatus.Pending;
        CreatedAt = now;
    }

    // ---- BERAT: the four methods below are decision lines (state transitions). ----

    /// <summary>The single home of the Pending rule. Throws <c>RequestErrors.NotPending()</c> (409) unless Pending.</summary>
    internal void EnsurePending()
    {
        throw new NotImplementedException();
    }

    /// <summary>Pending → Accepted; sets <see cref="ClosedAt"/>. Uses <see cref="EnsurePending"/>.</summary>
    internal void MarkAccepted(DateTimeOffset now)
    {
        throw new NotImplementedException();
    }

    /// <summary>Pending → Rejected; sets <see cref="ClosedAt"/>. Uses <see cref="EnsurePending"/>.</summary>
    internal void MarkRejected(DateTimeOffset now)
    {
        throw new NotImplementedException();
    }

    /// <summary>Pending → Withdrawn; sets <see cref="ClosedAt"/>. Uses <see cref="EnsurePending"/>.</summary>
    internal void MarkWithdrawn(DateTimeOffset now)
    {
        throw new NotImplementedException();
    }
}
