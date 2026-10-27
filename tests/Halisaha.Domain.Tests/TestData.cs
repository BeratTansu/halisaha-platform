using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Domain.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset StartsAt = Now.AddDays(2);
    public static readonly Guid Organizer = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static Guid NewPlayer() => Guid.NewGuid();

    public static Match OpenMatch(MatchType type = MatchType.Mixed, int capacity = 10, int external = 0) =>
        Match.Create(Organizer, venueId: 1, type, StartsAt, capacity, external, "Cuma 21:00 eksik var", Now);

    /// <summary>A completed match whose given players are on the roster.</summary>
    public static Match CompletedMatch(DateTimeOffset completedAt, params Guid[] players)
    {
        var match = OpenMatch();
        foreach (var p in players) match.AddParticipant(p, Now);
        match.Complete(Organizer, completedAt);
        return match;
    }
}
