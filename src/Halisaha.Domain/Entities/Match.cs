using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
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
    public DateTime StartsAt { get; private set; }
    public int Capacity { get; private set; }
    public int ExternalPlayerCount { get; private set; }
    public MatchStatus Status { get; private set; }
    public string? Description { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Match() { } // EF Core

    public int MissingPlayers => Capacity - (ExternalPlayerCount + _participations.Count);
    public bool IsFull => MissingPlayers <= 0;
    public bool HasStarted(DateTime now) => now >= StartsAt;
    public bool IsOrganizer(Guid userId) => OrganizerUserId == userId;
    public bool IsParticipant(Guid playerId) => _participations.Any(p => p.PlayerId == playerId);
    public MatchParticipation? ParticipationOf(Guid playerId) => _participations.FirstOrDefault(p => p.PlayerId == playerId);

    public static Match Create(Guid organizerUserId, int venueId, MatchType type, DateTime startsAt, int capacity, int externalPlayerCount,
        string? description, DateTime now)
    {
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

    private static void ValidateCapacity(MatchType type, int capacity, int externalPlayerCount, int rosterCount)
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

        if (capacity < (externalPlayerCount + rosterCount))
        {
            throw DomainException.Conflict("match.capacity_exceeded", "Capacity cannot be less than the players already in the match.");
        }
    }
}

