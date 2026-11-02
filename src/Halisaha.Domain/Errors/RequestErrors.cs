using Halisaha.Domain.Common;

namespace Halisaha.Domain.Errors;

/// <summary>
/// Every error a match request can raise, in one place: kind, code and message travel together.
/// Infrastructure throws <see cref="PendingExists"/> too, when the pending-request index rejects a race.
/// </summary>
public static class RequestErrors
{
    public static DomainException NotPending() =>
        DomainException.Conflict("request.not_pending", "This request is already closed.");

    public static DomainException NotResponder() =>
        DomainException.Forbidden("request.not_responder", "Only the receiving side can answer this request.");

    public static DomainException NotCreator() =>
        DomainException.Forbidden("request.not_creator", "Only the side that sent this request can withdraw it.");

    public static DomainException PendingExists() =>
        DomainException.Conflict("request.pending_exists", "There is already an open request between this player and this match.");

    public static DomainException RejectedBefore() =>
        DomainException.Conflict("request.rejected_before", "This request was rejected before and cannot be sent again for this match.");
}
