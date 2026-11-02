using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Errors;
using Halisaha.Domain.Rules;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Domain.Entities;

public class Match
{
    private readonly List<MatchParticipation> _participations = new();
    public IReadOnlyCollection<MatchParticipation> Participations => _participations;

    public Guid Id { get; private set; }
    public int VenueId { get; private set; }
    public Venue? Venue { get; private set; }
    public Guid OrganizerUserId { get; private set; }
    public MatchType Type { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public int Capacity { get; private set; }
    public int ExternalPlayerCount { get; private set; }
    public MatchStatus Status { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Match() { } // EF Core

    public int MissingPlayers => Capacity - (ExternalPlayerCount + _participations.Count);
    public bool IsFull => MissingPlayers <= 0;
    public bool HasStarted(DateTimeOffset now) => now >= StartsAt;
    public bool IsOrganizer(Guid userId) => OrganizerUserId == userId;
    public bool IsParticipant(Guid playerId) => _participations.Any(p => p.PlayerId == playerId);
    public MatchParticipation? ParticipationOf(Guid playerId) => _participations.FirstOrDefault(p => p.PlayerId == playerId);

    internal void EnsureOpenAndNotStarted(DateTimeOffset now)
    {
        if (Status != MatchStatus.Open)
        {
            throw DomainException.Conflict("match.not_open", "The match is not open.");
        }

        if (HasStarted(now))
        {
            throw DomainException.Conflict("match.started", "The match has already started.");
        }
    }

    internal void AddParticipant(Guid playerId, DateTimeOffset now)
    {
        if (playerId == Guid.Empty)
        {
            throw MatchErrors.PlayerRequired();
        }

        EnsureOpenAndNotStarted(now);

        if (IsParticipant(playerId))
        {
            throw MatchErrors.AlreadyParticipant();
        }

        if (IsFull)
        {
            throw DomainException.Conflict("match.full", "The match is full.");
        }

        _participations.Add(new MatchParticipation(Id, playerId, now));
    }

    public void Withdraw(Guid playerId, DateTimeOffset now)
    {
        if (IsOrganizer(playerId))
        {
            throw DomainException.Conflict("match.organizer_cannot_withdraw", "The organizer cannot leave the match; cancel it instead.");
        }

        var participation = ParticipationOf(playerId)
            ?? throw DomainException.Conflict("match.not_participant", "The player is not in the match.");

        EnsureOpenAndNotStarted(now);

        _participations.Remove(participation);
    }

    public bool IsWithoutResult(DateTimeOffset now)
    {
        return Status == MatchStatus.Open
            && now > StartsAt + MatchRules.CompletionWindow;
    }

    public bool IsAttendanceOpen(DateTimeOffset now)
    {
        return Status == MatchStatus.Completed
            && CompletedAt.HasValue
            && now >= CompletedAt.Value
            && now <= CompletedAt.Value + MatchRules.AttendanceWindow;
    }

    public bool IsRatingOpen(DateTimeOffset now)
    {
        return Status == MatchStatus.Completed
            && CompletedAt.HasValue
            && now >= CompletedAt.Value
            && now <= CompletedAt.Value + MatchRules.RatingWindow;
    }

    public void UpdateCapacity(Guid actorId, int capacity, int externalPlayerCount, DateTimeOffset now)
    {
        EnsureOrganizer(actorId);

        EnsureOpenAndNotStarted(now);

        ValidateCapacity(Type, capacity, externalPlayerCount, rosterCount: _participations.Count);

        Capacity = capacity;
        ExternalPlayerCount = externalPlayerCount;
    }

    public void Cancel(Guid actorId, DateTimeOffset now)
    {
        EnsureOrganizer(actorId);

        EnsureOpenAndNotStarted(now);

        Status = MatchStatus.Cancelled;

        CancelledAt = now;
    }

    public void Complete(Guid actorId, DateTimeOffset now)
    {
        EnsureOrganizer(actorId);

        if (Status != MatchStatus.Open)
        {
            throw DomainException.Conflict("match.not_open", "The match is not open.");
        }

        if (now < StartsAt)
        {
            throw DomainException.Conflict("match.not_started", "The match has not started yet.");
        }

        if (now > StartsAt + MatchRules.CompletionWindow)
        {
            throw DomainException.Conflict("match.completion_window_closed", "The completion window for this match has closed.");
        }

        // Look the organizer up before changing anything, so a broken invariant can't leave the match half-completed.
        var organizer = ParticipationOf(OrganizerUserId)
            ?? throw new InvalidOperationException($"Match {Id} has no participation for its organizer.");

        Status = MatchStatus.Completed;
        CompletedAt = now;
        organizer.SetAttendance(Attendance.Present);
        // Completing the match declares the organizer's own attendance.
    }

    public void RecordAttendance(Guid actorId, Guid playerId, Attendance attendance, DateTimeOffset now)
    {
        // Shape
        if (playerId == Guid.Empty)
        {
            throw MatchErrors.PlayerRequired();
        }

        if (attendance is not (Attendance.Present or Attendance.Absent))
        {
            throw DomainException.Validation("attendance.invalid_value", "Attendance must be Present or Absent.");
        }

        // Who
        EnsureOrganizer(actorId);

        // When
        if (Status != MatchStatus.Completed)
        {
            throw DomainException.Conflict("attendance.match_not_completed", "Attendance can only be recorded for a completed match.");
        }

        if (!IsAttendanceOpen(now))
        {
            throw DomainException.Conflict("attendance.window_closed", "The attendance window for this match has closed.");
        }

        // What
        if (IsOrganizer(playerId))
        {
            throw DomainException.Validation("attendance.cannot_record_organizer", "The organizer's attendance is set when the match is completed.");
        }

        var participation = ParticipationOf(playerId)
            ?? throw DomainException.Conflict("match.not_participant", "The player is not in the match.");

        participation.SetAttendance(attendance);
    }

    internal void EnsureOrganizer(Guid actorId)
    {
        if (!IsOrganizer(actorId))
        {
            throw DomainException.Forbidden("match.not_organizer", "Only the organizer can do this.");
        }
    }

    public static Match Create(
        Guid organizerUserId,
        int venueId,
        MatchType type,
        DateTimeOffset startsAt,
        int capacity,
        int externalPlayerCount,
        string? description,
        DateTimeOffset now)
    {
        if (organizerUserId == Guid.Empty)
        {
            throw DomainException.Validation("match.organizer_required", "Organizer is required.");
        }

        if (now >= startsAt)
        {
            throw DomainException.Validation("match.start_in_past", "Match must start in the future.");
        }

        ValidateCapacity(type, capacity, externalPlayerCount, rosterCount: 1);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            VenueId = venueId,
            OrganizerUserId = organizerUserId,
            Type = type,
            StartsAt = startsAt,
            Capacity = capacity,
            ExternalPlayerCount = externalPlayerCount,
            Description = description,
            Status = MatchStatus.Open,
            CreatedAt = now,
        };

        match._participations.Add(new MatchParticipation(match.Id, organizerUserId, now));

        return match;
    }

    private static void ValidateCapacity(
        MatchType type,
        int capacity,
        int externalPlayerCount,
        int rosterCount)
    {
        if (!Enum.IsDefined(type))
        {
            throw DomainException.Validation("match.invalid_type", "Match type must be one of the defined values.");
        }

        if (capacity is < MatchRules.MinCapacity or > MatchRules.MaxCapacity)
        {
            throw DomainException.Validation("match.invalid_capacity", $"Capacity must be between {MatchRules.MinCapacity} and {MatchRules.MaxCapacity}.");
        }

        if (type == MatchType.Mixed && capacity % 2 != 0)
        {
            throw DomainException.Validation("match.mixed_capacity_odd", "Mixed match capacity must be even.");
        }

        if (externalPlayerCount < 0)
        {
            throw DomainException.Validation("match.invalid_external_count", "External player count cannot be negative.");
        }

        if (externalPlayerCount > capacity - rosterCount)
        {
            throw DomainException.Conflict("match.capacity_exceeded", "Capacity cannot be less than the players already in the match.");
        }
    }
}
