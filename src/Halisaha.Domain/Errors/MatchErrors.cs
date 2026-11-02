using Halisaha.Domain.Common;

namespace Halisaha.Domain.Errors;

/// <summary>
/// Match errors that more than one class raises. The remaining match codes still live as literals
/// in <see cref="Entities.Match"/> and move here in a separate refactor.
/// </summary>
public static class MatchErrors
{
    /// <summary>Raised by <c>Match.AddParticipant</c>, <c>Match.RecordAttendance</c> and <c>RequestPolicy</c>.</summary>
    public static DomainException PlayerRequired() =>
        DomainException.Validation("match.player_required", "Player is required.");

    /// <summary>Raised by <c>Match.AddParticipant</c> and by <c>RequestPolicy</c> when sending a request.</summary>
    public static DomainException AlreadyParticipant() =>
        DomainException.Conflict("match.already_participant", "The player is already in the match.");
}
