using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Domain.Entities;

/// <summary>
/// A match is an event and the organizer's area of responsibility.
/// Mixed: the whole match. WithOpponent: only the organizer's side; the opponent is outside the system.
/// </summary>
public sealed class Match
{
    private readonly List<MatchParticipation> _participations = new();

    public Guid Id { get; private set; }
    public int VenueId { get; private set; }
    public Venue? Venue { get; private set; }
    public Guid OrganizerUserId { get; private set; }
    public MatchType Type { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }

    /// <summary>Total players in the organizer's scope, organizer included.</summary>
    public int Capacity { get; private set; }

    /// <summary>Players without a platform account. Does not include the organizer.</summary>
    public int ExternalPlayerCount { get; private set; }

    public MatchStatus Status { get; private set; }

    /// <summary>The organizer's original text. AI output will be stored separately and never overwrite it.</summary>
    public string? Description { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<MatchParticipation> Participations => _participations;

    private Match() { }

    public static Match Create(
        Guid organizerUserId, int venueId, MatchType type, DateTimeOffset startsAt,
        int capacity, int externalPlayerCount, string? description, DateTimeOffset now)
    {
        if (startsAt <= now)
            throw DomainException.Validation("match.start_in_past", "A match must start in the future.");

        var match = new Match
        {
            Id = Guid.NewGuid(),
            OrganizerUserId = organizerUserId,
            VenueId = venueId,
            Type = type,
            StartsAt = startsAt.ToUniversalTime(),
            Status = MatchStatus.Open,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = now,
        };

        // The organizer always plays, so the roster starts with them.
        ValidateCapacity(type, capacity, externalPlayerCount, rosterCount: 1);
        match.Capacity = capacity;
        match.ExternalPlayerCount = externalPlayerCount;
        match._participations.Add(new MatchParticipation(match.Id, organizerUserId, now));
        return match;
    }

    // ---- Computed state (never stored) ----

    public int MissingPlayers => Capacity - ExternalPlayerCount - _participations.Count;
    public bool IsFull => MissingPlayers <= 0;
    public bool HasStarted(DateTimeOffset now) => now >= StartsAt;

    /// <summary>Still open a week after the start: no attendance or ratings, affects no one.</summary>
    public bool IsWithoutResult(DateTimeOffset now) =>
        Status == MatchStatus.Open && now > StartsAt + MatchRules.CompletionWindow;

    public bool IsOrganizer(Guid userId) => userId == OrganizerUserId;
    public bool IsParticipant(Guid playerId) => _participations.Any(p => p.PlayerId == playerId);

    public MatchParticipation? ParticipationOf(Guid playerId) =>
        _participations.SingleOrDefault(p => p.PlayerId == playerId);

    public bool IsRatingOpen(DateTimeOffset now) =>
        Status == MatchStatus.Completed && CompletedAt is not null && now <= CompletedAt + MatchRules.RatingWindow;

    // ---- Guards ----

    public void EnsureOrganizer(Guid userId)
    {
        if (!IsOrganizer(userId))
            throw DomainException.Forbidden("match.not_organizer", "Only the organizer can manage this match.");
    }

    /// <summary>Requests can be created or accepted only while the match is open and has not started.</summary>
    public void EnsureOpenForJoining(DateTimeOffset now)
    {
        if (Status != MatchStatus.Open)
            throw DomainException.Conflict("match.not_open", "The match is no longer open.");
        if (HasStarted(now))
            throw DomainException.Conflict("match.started", "The match has already started.");
    }

    // ---- Roster ----

    /// <summary>
    /// Adds an accepted player. Capacity is checked here, at acceptance time.
    /// The caller must hold a lock on this match row so two acceptances cannot both take the last slot.
    /// </summary>
    public MatchParticipation AddParticipant(Guid playerId, DateTimeOffset now)
    {
        EnsureOpenForJoining(now);
        if (IsParticipant(playerId))
            throw DomainException.Conflict("match.already_participant", "The player is already in this match.");
        if (IsFull)
            throw DomainException.Conflict("match.full", "The match is full.");

        var participation = new MatchParticipation(Id, playerId, now);
        _participations.Add(participation);
        return participation;
    }

    /// <summary>A player can leave only before the start. The organizer cannot leave, only cancel.</summary>
    public void Withdraw(Guid playerId, DateTimeOffset now)
    {
        if (IsOrganizer(playerId))
            throw DomainException.Conflict("match.organizer_cannot_withdraw", "The organizer cannot leave the match; cancel it instead.");
        var participation = ParticipationOf(playerId)
            ?? throw DomainException.Conflict("match.not_participant", "The player is not in this match.");
        if (Status != MatchStatus.Open)
            throw DomainException.Conflict("match.not_open", "The match is no longer open.");
        if (HasStarted(now))
            throw DomainException.Conflict("match.started", "Players cannot leave after the start time.");

        _participations.Remove(participation);
    }

    public void UpdateCapacity(Guid actorId, int capacity, int externalPlayerCount, DateTimeOffset now)
    {
        EnsureOrganizer(actorId);
        EnsureOpenForJoining(now);
        ValidateCapacity(Type, capacity, externalPlayerCount, _participations.Count);
        Capacity = capacity;
        ExternalPlayerCount = externalPlayerCount;
    }

    // ---- Lifecycle ----

    public void Cancel(Guid actorId, DateTimeOffset now)
    {
        EnsureOrganizer(actorId);
        if (Status != MatchStatus.Open)
            throw DomainException.Conflict("match.not_open", "Only an open match can be cancelled.");
        Status = MatchStatus.Cancelled;
        CancelledAt = now;
    }

    /// <summary>Allowed after the start time and at most 7 days later. Never done automatically.</summary>
    public void Complete(Guid actorId, DateTimeOffset now)
    {
        EnsureOrganizer(actorId);
        if (Status != MatchStatus.Open)
            throw DomainException.Conflict("match.not_open", "Only an open match can be completed.");
        if (!HasStarted(now))
            throw DomainException.Conflict("match.not_started", "A match can be completed only after its start time.");
        if (now > StartsAt + MatchRules.CompletionWindow)
            throw DomainException.Conflict("match.completion_window_closed", "A match can be completed at most 7 days after its start.");
        Status = MatchStatus.Completed;
        CompletedAt = now;
    }

    /// <summary>The organizer records attendance for their own scope, within 48 hours of completion.</summary>
    public void RecordAttendance(Guid actorId, Guid playerId, Attendance attendance, DateTimeOffset now)
    {
        EnsureOrganizer(actorId);
        if (Status != MatchStatus.Completed || CompletedAt is null)
            throw DomainException.Conflict("attendance.match_not_completed", "Attendance can be recorded only for a completed match.");
        if (now > CompletedAt + MatchRules.AttendanceWindow)
            throw DomainException.Conflict("attendance.window_closed", "Attendance can be edited for 48 hours after completion.");
        if (!Enum.IsDefined(attendance))
            throw DomainException.Validation("attendance.invalid", "Unknown attendance value.");

        var participation = ParticipationOf(playerId)
            ?? throw DomainException.Conflict("match.not_participant", "The player is not in this match.");
        participation.SetAttendance(attendance);
    }

    private static void ValidateCapacity(MatchType type, int capacity, int externalPlayerCount, int rosterCount)
    {
        if (!Enum.IsDefined(type))
            throw DomainException.Validation("match.invalid_type", "Unknown match type.");
        if (capacity is < MatchRules.MinCapacity or > MatchRules.MaxCapacity)
            throw DomainException.Validation("match.invalid_capacity", $"Capacity must be between {MatchRules.MinCapacity} and {MatchRules.MaxCapacity}.");
        if (type == MatchType.Mixed && capacity % 2 != 0)
            throw DomainException.Validation("match.mixed_capacity_odd", "A mixed match needs an even capacity.");
        if (externalPlayerCount < 0)
            throw DomainException.Validation("match.invalid_external_count", "External player count cannot be negative.");
        if (externalPlayerCount + rosterCount > capacity)
            throw DomainException.Conflict("match.capacity_exceeded", "External players and accepted players cannot exceed the capacity.");
    }
}
