using Halisaha.Domain.Entities;

namespace Halisaha.Domain.Rules;

/// <summary>
/// Rules that need both a match and a request: who may act, when, and in which order the checks run.
/// Error priority: Shape (400) → Who (403) → When (409) → What (400/409).
/// The domain knows nothing about locks; the application layer loads the data inside a transaction.
/// </summary>
public static class RequestPolicy
{
    // ---- BERAT: every method body in this class is a decision line. Signatures are the agreed contract. ----

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
        throw new NotImplementedException();
    }

    /// <summary>
    /// The organizer invites a player. Same checks as <see cref="Apply"/>, plus Who: only the organizer (403 <c>match.not_organizer</c>).
    /// </summary>
    public static MatchRequest Invite(Match match, Guid actorId, Guid playerId, IReadOnlyCollection<MatchRequest> pairHistory, DateTimeOffset now)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The receiving side accepts: the player joins the roster and the request becomes Accepted, both or neither.
    /// Order: responder → open and not started → pending → AddParticipant (may throw <c>match.full</c>) → MarkAccepted.
    /// Every check runs before the first change.
    /// </summary>
    public static void Accept(Match match, MatchRequest request, Guid actorId, DateTimeOffset now)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The receiving side rejects. Order: responder → open and not started → pending. A full match does not block it.
    /// </summary>
    public static void Reject(Match match, MatchRequest request, Guid actorId, DateTimeOffset now)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The sending side takes the request back. Order: creator → open and not started → pending. A full match does not block it.
    /// </summary>
    public static void Withdraw(Match match, MatchRequest request, Guid actorId, DateTimeOffset now)
    {
        throw new NotImplementedException();
    }
}
