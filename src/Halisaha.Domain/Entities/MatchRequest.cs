using Halisaha.Domain.Enums;
using Halisaha.Domain.Errors;

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

    internal void EnsurePending()
    {
        if (Status != RequestStatus.Pending)
        {
            throw RequestErrors.NotPending();
        }
    }

    internal void MarkAccepted(DateTimeOffset now)
    {
        EnsurePending();

        Status = RequestStatus.Accepted;
        ClosedAt = now;
    }

    internal void MarkRejected(DateTimeOffset now)
    {
        EnsurePending();

        Status = RequestStatus.Rejected;
        ClosedAt = now;
    }

    internal void MarkWithdrawn(DateTimeOffset now)
    {
        EnsurePending();

        Status = RequestStatus.Withdrawn;
        ClosedAt = now;
    }
}
