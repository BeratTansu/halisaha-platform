using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Domain.Tests;

/// <summary>Shared fixed values so every test uses the same clock and organizer.</summary>
public static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset StartsAt = Now.AddDays(2);
    public static readonly Guid Organizer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Mert = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Emre = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>When <see cref="CompletedMatch"/> is completed: one hour after kick-off.</summary>
    public static readonly DateTimeOffset CompletedAt = StartsAt.AddHours(1);

    /// <summary>The smallest step past a boundary.</summary>
    public static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    /// <summary>A valid open 14-player mixed match with 2 external players; only the organizer has joined.</summary>
    public static Match OpenMatch(int capacity = 14, int externalPlayerCount = 2, MatchType type = MatchType.Mixed)
        => Match.Create(Organizer, venueId: 1, type, StartsAt, capacity, externalPlayerCount, "Kramponsuz gelmeyin.", Now);

    /// <summary>The open match with Mert joined, completed by the organizer at <see cref="CompletedAt"/>.</summary>
    public static Match CompletedMatch()
    {
        var match = OpenMatch();
        match.AddParticipant(Mert, Now);
        match.Complete(Organizer, CompletedAt);
        return match;
    }
}
