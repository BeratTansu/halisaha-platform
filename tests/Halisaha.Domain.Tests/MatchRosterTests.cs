using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Tests;

public class MatchRosterTests
{
    private static readonly Guid Mert = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Mert joins: one more participant, one less missing player, attendance not recorded yet.
    [Fact]
    public void AddParticipant_WhenOpen_AddsPlayer()
    {
        var match = TestData.OpenMatch(capacity: 14, externalPlayerCount: 2);

        match.AddParticipant(Mert, TestData.Now);

        Assert.True(match.IsParticipant(Mert));
        Assert.Equal(10, match.MissingPlayers);
        Assert.Equal(Attendance.NotRecorded, match.ParticipationOf(Mert)!.Attendance);
    }

    [Fact]
    public void AddParticipant_Twice_ThrowsAlreadyParticipant()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(Mert, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.AddParticipant(Mert, TestData.Now));
        Assert.Equal("match.already_participant", ex.Code);
    }

    // Capacity 2 with no external players: organizer + Mert fill it, a third player is rejected.
    [Fact]
    public void AddParticipant_WhenFull_ThrowsFull()
    {
        var match = TestData.OpenMatch(capacity: 2, externalPlayerCount: 0);
        match.AddParticipant(Mert, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.AddParticipant(Guid.NewGuid(), TestData.Now));
        Assert.Equal("match.full", ex.Code);
        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
    }

    // Exactly at kick-off counts as started.
    [Fact]
    public void AddParticipant_AtStartTime_ThrowsStarted()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.AddParticipant(Mert, TestData.StartsAt));
        Assert.Equal("match.started", ex.Code);
    }

    [Fact]
    public void Withdraw_WhenParticipant_RemovesPlayer()
    {
        var match = TestData.OpenMatch(capacity: 14, externalPlayerCount: 2);
        match.AddParticipant(Mert, TestData.Now);

        match.Withdraw(Mert, TestData.Now);

        Assert.False(match.IsParticipant(Mert));
        Assert.Equal(11, match.MissingPlayers);
    }

    [Fact]
    public void Withdraw_Organizer_Throws()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.Withdraw(TestData.Organizer, TestData.Now));
        Assert.Equal("match.organizer_cannot_withdraw", ex.Code);
    }

    [Fact]
    public void Withdraw_NotParticipant_Throws()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.Withdraw(Mert, TestData.Now));
        Assert.Equal("match.not_participant", ex.Code);
    }

    // Leaving after kick-off is not allowed; Mert stays in the roster.
    [Fact]
    public void Withdraw_AfterStart_ThrowsAndKeepsPlayer()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(Mert, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.Withdraw(Mert, TestData.StartsAt.AddMinutes(30)));
        Assert.Equal("match.started", ex.Code);
        Assert.True(match.IsParticipant(Mert));
    }

    [Fact]
    public void AddParticipant_WithEmptyPlayer_Throws()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(
            () => match.AddParticipant(Guid.Empty, TestData.Now));

        Assert.Equal("match.player_required", ex.Code);
    }
}