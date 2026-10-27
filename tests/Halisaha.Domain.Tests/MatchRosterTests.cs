using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using MatchType = Halisaha.Domain.Enums.MatchType;
using static Halisaha.Domain.Tests.TestData;

namespace Halisaha.Domain.Tests;

public class MatchRosterTests
{
    [Fact]
    public void Last_free_slot_can_be_taken()
    {
        var match = OpenMatch(MatchType.WithOpponent, capacity: 3);
        match.AddParticipant(NewPlayer(), Now);
        match.AddParticipant(NewPlayer(), Now);
        Assert.True(match.IsFull);
    }

    [Fact]
    public void Full_match_rejects_another_player()
    {
        var match = OpenMatch(MatchType.WithOpponent, capacity: 2);
        match.AddParticipant(NewPlayer(), Now);
        var ex = Assert.Throws<DomainException>(() => match.AddParticipant(NewPlayer(), Now));
        Assert.Equal("match.full", ex.Code);
        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
    }

    [Fact]
    public void Same_player_cannot_join_twice()
    {
        var match = OpenMatch();
        var player = NewPlayer();
        match.AddParticipant(player, Now);
        var ex = Assert.Throws<DomainException>(() => match.AddParticipant(player, Now));
        Assert.Equal("match.already_participant", ex.Code);
    }

    [Fact]
    public void Nobody_can_join_after_the_start()
    {
        var match = OpenMatch();
        var ex = Assert.Throws<DomainException>(() => match.AddParticipant(NewPlayer(), StartsAt));
        Assert.Equal("match.started", ex.Code);
    }

    [Fact]
    public void Player_can_withdraw_one_minute_before_start_and_frees_a_slot()
    {
        var match = OpenMatch(MatchType.WithOpponent, capacity: 2);
        var player = NewPlayer();
        match.AddParticipant(player, Now);
        match.Withdraw(player, StartsAt.AddMinutes(-1));
        Assert.False(match.IsParticipant(player));
        Assert.False(match.IsFull);
    }

    [Fact]
    public void Player_cannot_withdraw_at_start_time()
    {
        var match = OpenMatch();
        var player = NewPlayer();
        match.AddParticipant(player, Now);
        var ex = Assert.Throws<DomainException>(() => match.Withdraw(player, StartsAt));
        Assert.Equal("match.started", ex.Code);
    }

    [Fact]
    public void Organizer_cannot_withdraw()
    {
        var match = OpenMatch();
        var ex = Assert.Throws<DomainException>(() => match.Withdraw(Organizer, Now));
        Assert.Equal("match.organizer_cannot_withdraw", ex.Code);
    }

    [Fact]
    public void Capacity_cannot_be_lowered_below_the_current_roster()
    {
        var match = OpenMatch(MatchType.WithOpponent, capacity: 5);
        match.AddParticipant(NewPlayer(), Now);
        match.AddParticipant(NewPlayer(), Now);
        var ex = Assert.Throws<DomainException>(() => match.UpdateCapacity(Organizer, 4, 2, Now));
        Assert.Equal("match.capacity_exceeded", ex.Code);
    }

    [Fact]
    public void Only_the_organizer_can_change_capacity()
    {
        var match = OpenMatch();
        var ex = Assert.Throws<DomainException>(() => match.UpdateCapacity(NewPlayer(), 12, 0, Now));
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }
}
