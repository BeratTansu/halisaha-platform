using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Rules;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Domain.Tests;

public class MatchCreationTests
{
    // Happy path: an open match whose only participant is the organizer, with no team and no attendance yet.
    [Fact]
    public void Create_WithValidData_OpensMatchWithOrganizerAsFirstParticipant()
    {
        var match = TestData.OpenMatch();

        Assert.Equal(MatchStatus.Open, match.Status);
        Assert.Equal(TestData.Now, match.CreatedAt);
        Assert.Single(match.Participations);

        var organizer = match.ParticipationOf(TestData.Organizer);
        Assert.NotNull(organizer);
        Assert.Equal(match.Id, organizer.MatchId);
        Assert.Null(organizer.Team);
        Assert.Equal(Attendance.NotRecorded, organizer.Attendance);
    }

    // 14 capacity - 2 external - 1 organizer = 11 missing.
    [Fact]
    public void Create_WithExternalPlayers_ComputesMissingPlayers()
    {
        var match = TestData.OpenMatch(capacity: 14, externalPlayerCount: 2);

        Assert.Equal(11, match.MissingPlayers);
        Assert.False(match.IsFull);
    }

    // Start time equal to now counts as "in the past" too.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithStartNotInFuture_Throws(int minutesFromNow)
    {
        var startsAt = TestData.Now.AddMinutes(minutesFromNow);

        var ex = Assert.Throws<DomainException>(() =>
            Match.Create(TestData.Organizer, 1, MatchType.Mixed, startsAt, 14, 0, null, TestData.Now));
        Assert.Equal("match.start_in_past", ex.Code);
    }

    [Theory]
    [InlineData(MatchRules.MinCapacity - 1)]
    [InlineData(MatchRules.MaxCapacity + 2)] // +2 keeps it even, so only the range rule can fail
    public void Create_WithCapacityOutOfRange_Throws(int capacity)
    {
        var ex = Assert.Throws<DomainException>(() => TestData.OpenMatch(capacity: capacity, externalPlayerCount: 0));
        Assert.Equal("match.invalid_capacity", ex.Code);
    }

    // Mixed matches are split into two teams, so 13 is rejected...
    [Fact]
    public void Create_MixedWithOddCapacity_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => TestData.OpenMatch(capacity: 13, type: MatchType.Mixed));
        Assert.Equal("match.mixed_capacity_odd", ex.Code);
    }

    // ...but an opponent match counts only the organizer's own side, so 7 is fine.
    [Fact]
    public void Create_WithOpponentAndOddCapacity_Succeeds()
    {
        var match = TestData.OpenMatch(capacity: 7, externalPlayerCount: 0, type: MatchType.WithOpponent);

        Assert.Equal(7, match.Capacity);
    }

    [Fact]
    public void Create_WithUndefinedType_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => TestData.OpenMatch(type: (MatchType)99));
        Assert.Equal("match.invalid_type", ex.Code);
    }

    [Fact]
    public void Create_WithNegativeExternalCount_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => TestData.OpenMatch(externalPlayerCount: -1));
        Assert.Equal("match.invalid_external_count", ex.Code);
    }

    // Boundary: 13 external + organizer = 14 fills the match exactly; 14 external overflows it.
    [Fact]
    public void Create_WhenExternalPlayersFillTheRest_IsFull()
    {
        var match = TestData.OpenMatch(capacity: 14, externalPlayerCount: 13);

        Assert.True(match.IsFull);
    }

    [Fact]
    public void Create_WhenExternalPlayersLeaveNoRoomForOrganizer_ThrowsConflict()
    {
        var ex = Assert.Throws<DomainException>(() => TestData.OpenMatch(capacity: 14, externalPlayerCount: 14));
        Assert.Equal("match.capacity_exceeded", ex.Code);
        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
    }
}