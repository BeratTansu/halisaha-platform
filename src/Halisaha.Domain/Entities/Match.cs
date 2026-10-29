using Halisaha.Domain.Enums;
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
}

