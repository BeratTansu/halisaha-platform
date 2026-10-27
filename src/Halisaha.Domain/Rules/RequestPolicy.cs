using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Rules;

/// <summary>
/// Rules that span a match and every request between that match and one player.
/// The application layer loads the data (under a lock on the match) and calls these methods.
/// </summary>
public static class RequestPolicy
{
    /// <summary>
    /// Sends an application (player → match) or an invitation (organizer → player).
    /// If the other side already has a pending request, both sides want the same thing:
    /// that request is accepted instead and no new request is created.
    /// </summary>
    /// <param name="existingForPair">Every request ever made between this match and this player.</param>
    public static SendResult Send(
        Match match, Guid playerId, RequestType type,
        IReadOnlyCollection<MatchRequest> existingForPair, DateTimeOffset now)
    {
        match.EnsureOpenForJoining(now);

        if (match.IsParticipant(playerId))
            throw DomainException.Conflict("request.already_participant", "The player is already in this match.");

        // A rejection blocks the same kind of request again: a rejected application
        // cannot be re-sent, and a rejected invitation cannot be re-sent.
        if (existingForPair.Any(r => r.Type == type && r.Status == RequestStatus.Rejected))
            throw DomainException.Conflict("request.rejected_before", "This request was rejected before and cannot be sent again.");

        var pending = existingForPair.SingleOrDefault(r => r.Status == RequestStatus.Pending);
        if (pending is not null)
        {
            if (pending.Type == type)
                throw DomainException.Conflict("request.already_pending", "There is already a pending request.");

            // Cross request: the other side already asked. Treat this as acceptance.
            Accept(match, pending, now);
            return new SendResult(pending, AcceptedExisting: true);
        }

        return new SendResult(new MatchRequest(match.Id, playerId, type, now), AcceptedExisting: false);
    }

    /// <summary>
    /// Accepts a pending request: the request becomes Accepted and the player joins the roster,
    /// both or neither. The caller saves both in one transaction while holding the match lock.
    /// </summary>
    public static MatchParticipation Accept(Match match, MatchRequest request, DateTimeOffset now)
    {
        if (request.MatchId != match.Id)
            throw DomainException.Validation("request.wrong_match", "The request does not belong to this match.");
        if (request.Status != RequestStatus.Pending)
            throw DomainException.Conflict("request.not_pending", "This request is already closed.");

        var participation = match.AddParticipant(request.PlayerId, now); // checks open, started, capacity
        request.MarkAccepted(now);
        return participation;
    }

    /// <summary>Accept on behalf of an actor, checking that the actor is the receiving side.</summary>
    public static MatchParticipation AcceptBy(Guid actorId, Match match, MatchRequest request, DateTimeOffset now)
    {
        if (!request.IsResponder(actorId, match.OrganizerUserId))
            throw DomainException.Forbidden("request.not_responder", "Only the receiving side can accept this request.");
        return Accept(match, request, now);
    }
}

public sealed record SendResult(MatchRequest Request, bool AcceptedExisting);
