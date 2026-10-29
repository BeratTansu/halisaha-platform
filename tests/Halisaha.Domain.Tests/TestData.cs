using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Domain.Tests;

/// <summary>Shared fixed values so every test uses the same clock and organizer.</summary>
public static class TestData
{
    public static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime StartsAt = Now.AddDays(2);
    public static readonly Guid Organizer = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>A valid open 14-player mixed match with 2 external players; only the organizer has joined.</summary>
    public static Match OpenMatch(int capacity = 14, int externalPlayerCount = 2, MatchType type = MatchType.Mixed)
        => Match.Create(Organizer, venueId: 1, type, StartsAt, capacity, externalPlayerCount, "Kramponsuz gelmeyin.", Now);
}