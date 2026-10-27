using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Entities;

/// <summary>
/// The history of how a player and a match came together.
/// Application: player asks, organizer answers. Invitation: organizer asks, player answers.
/// End states (Accepted, Rejected, Withdrawn) are final.
/// </summary>
public sealed class MatchRequest
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid PlayerId { get; private set; }
    public RequestType Type { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    private MatchRequest() { }

    internal MatchRequest(Guid matchId, Guid playerId, RequestType type, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        MatchId = matchId;
        PlayerId = playerId;
        Type = type;
        Status = RequestStatus.Pending;
        CreatedAt = now;
    }

    /// <summary>The side that answers: organizer for applications, player for invitations.</summary>
    public bool IsResponder(Guid actorId, Guid organizerId) =>
        Type == RequestType.Application ? actorId == organizerId : actorId == PlayerId;

    /// <summary>The side that created it: player for applications, organizer for invitations.</summary>
    public bool IsCreator(Guid actorId, Guid organizerId) =>
        Type == RequestType.Application ? actorId == PlayerId : actorId == organizerId;

    /// <summary>Marks the request accepted. The roster change happens in the same transaction via <see cref="Match.AddParticipant"/>.</summary>
    internal void MarkAccepted(DateTimeOffset now) => Close(RequestStatus.Accepted, now);

    public void Reject(Guid actorId, Guid organizerId, DateTimeOffset now)
    {
        if (!IsResponder(actorId, organizerId))
            throw DomainException.Forbidden("request.not_responder", "Only the receiving side can reject this request.");
        Close(RequestStatus.Rejected, now);
    }

    public void Withdraw(Guid actorId, Guid organizerId, DateTimeOffset now)
    {
        if (!IsCreator(actorId, organizerId))
            throw DomainException.Forbidden("request.not_creator", "Only the side that sent this request can withdraw it.");
        Close(RequestStatus.Withdrawn, now);
    }

    private void Close(RequestStatus status, DateTimeOffset now)
    {
        if (Status != RequestStatus.Pending)
            throw DomainException.Conflict("request.not_pending", "This request is already closed.");
        Status = status;
        RespondedAt = now;
    }
}
