using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Errors;

namespace Halisaha.Domain.Rules;

/// <summary>
/// Rules that need both a match and a request: who may act, when, and in which order the checks run.
/// Error priority: Shape (400) → Who (403) → When (409) → What (400/409).
/// The domain knows nothing about locks; the application layer loads the data inside a transaction.
/// </summary>
public static class RequestPolicy
{
    /// <summary>
    /// The player (the signed-in user) applies to the match. Creates a new Pending application; never changes the roster.
    /// What-order: <c>match.already_participant</c> → <c>request.pending_exists</c> → <c>request.rejected_before</c>.
    /// </summary>
    /// <param name="pairHistory">
    /// Every request ever made between this match and this player (all types, all statuses).
    /// A row for another match or player is a programming error: <see cref="InvalidOperationException"/>.
    /// </param>
    public static MatchRequest Apply(Match match, Guid playerId, IReadOnlyCollection<MatchRequest> pairHistory, DateTimeOffset now)
    {
        // Shape
        EnsurePlayerGiven(playerId);

        // Who: the applicant is the signed-in user, so there is nothing to check.
        return Send(match, playerId, RequestType.Application, pairHistory, now);
    }

    /// <summary>
    /// The organizer invites a player. Same checks as <see cref="Apply"/>, plus Who: only the organizer (403 <c>match.not_organizer</c>).
    /// </summary>
    public static MatchRequest Invite(Match match, Guid actorId, Guid playerId, IReadOnlyCollection<MatchRequest> pairHistory, DateTimeOffset now)
    {
        // Shape
        EnsurePlayerGiven(playerId);

        // Who
        match.EnsureOrganizer(actorId);

        return Send(match, playerId, RequestType.Invitation, pairHistory, now);
    }

    /// <summary>
    /// The receiving side accepts: the player joins the roster and the request becomes Accepted, both or neither.
    /// Order: responder → open and not started → pending → AddParticipant (may throw <c>match.full</c>) → MarkAccepted.
    /// Every check runs before the first change.
    /// </summary>
    public static void Accept(Match match, MatchRequest request, Guid actorId, DateTimeOffset now)
    {
        EnsureSameMatch(match, request);

        // Who
        if (actorId != ResponderOf(match, request))
        {
            throw RequestErrors.NotResponder();
        }

        // When
        match.EnsureOpenAndNotStarted(now);

        // What
        request.EnsurePending();

        // Change: AddParticipant checks capacity; if the match is full the request stays Pending.
        match.AddParticipant(request.PlayerId, now);
        request.MarkAccepted(now);
    }

    /// <summary>
    /// The receiving side rejects. Order: responder → open and not started → pending. A full match does not block it.
    /// </summary>
    public static void Reject(Match match, MatchRequest request, Guid actorId, DateTimeOffset now)
    {
        EnsureSameMatch(match, request);

        // Who
        if (actorId != ResponderOf(match, request))
        {
            throw RequestErrors.NotResponder();
        }

        // When
        match.EnsureOpenAndNotStarted(now);

        // What (MarkRejected checks Pending through EnsurePending)
        request.MarkRejected(now);
    }

    /// <summary>
    /// The sending side takes the request back. Order: creator → open and not started → pending. A full match does not block it.
    /// </summary>
    public static void Withdraw(Match match, MatchRequest request, Guid actorId, DateTimeOffset now)
    {
        EnsureSameMatch(match, request);

        // Who
        if (actorId != CreatorOf(match, request))
        {
            throw RequestErrors.NotCreator();
        }

        // When
        match.EnsureOpenAndNotStarted(now);

        // What (MarkWithdrawn checks Pending through EnsurePending)
        request.MarkWithdrawn(now);
    }

    /// <summary>The checks Apply and Invite share once Shape and Who have passed.</summary>
    private static MatchRequest Send(
        Match match, Guid playerId, RequestType type,
        IReadOnlyCollection<MatchRequest> pairHistory, DateTimeOffset now)
    {
        EnsurePairHistory(match, playerId, pairHistory);

        // When
        match.EnsureOpenAndNotStarted(now);

        // What: the current state comes before the past.
        if (match.IsParticipant(playerId))
        {
            throw MatchErrors.AlreadyParticipant();
        }

        if (pairHistory.Any(request => request.Status == RequestStatus.Pending))
        {
            throw RequestErrors.PendingExists();
        }

        // Only a rejection in the same direction blocks: the side that said no can still open the other way.
        if (pairHistory.Any(request => request.Type == type && request.Status == RequestStatus.Rejected))
        {
            throw RequestErrors.RejectedBefore();
        }

        return new MatchRequest(match.Id, playerId, type, now);
    }

    /// <summary>Who answers the request: the organizer for an application, the player for an invitation.</summary>
    private static Guid ResponderOf(Match match, MatchRequest request)
    {
        return request.Type switch
        {
            RequestType.Application => match.OrganizerUserId,
            RequestType.Invitation => request.PlayerId,
            _ => throw new InvalidOperationException($"Unknown request type {request.Type}.")
        };
    }

    /// <summary>Who sent the request: the player for an application, the organizer for an invitation.</summary>
    private static Guid CreatorOf(Match match, MatchRequest request)
    {
        return request.Type switch
        {
            RequestType.Application => request.PlayerId,
            RequestType.Invitation => match.OrganizerUserId,
            _ => throw new InvalidOperationException($"Unknown request type {request.Type}.")
        };
    }

    private static void EnsurePlayerGiven(Guid playerId)
    {
        if (playerId == Guid.Empty)
        {
            throw MatchErrors.PlayerRequired();
        }
    }

    // The two guards below catch programming errors in the application layer, not user mistakes.

    private static void EnsureSameMatch(Match match, MatchRequest request)
    {
        if (request.MatchId != match.Id)
        {
            throw new InvalidOperationException($"Request {request.Id} does not belong to match {match.Id}.");
        }
    }

    private static void EnsurePairHistory(Match match, Guid playerId, IReadOnlyCollection<MatchRequest> pairHistory)
    {
        if (pairHistory.Any(request => request.MatchId != match.Id || request.PlayerId != playerId))
        {
            throw new InvalidOperationException($"Pair history for match {match.Id} and player {playerId} contains another pair's request.");
        }
    }
}
