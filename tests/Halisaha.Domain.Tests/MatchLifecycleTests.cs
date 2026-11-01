using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;

namespace Halisaha.Domain.Tests;

public class MatchLifecycleTests
{
    private static readonly DateTimeOffset CompletionDeadline = TestData.StartsAt + MatchRules.CompletionWindow;

    // ---------- UpdateCapacity ----------

    [Fact]
    public void UpdateCapacity_ByOrganizer_UpdatesCapacityAndExternalCount()
    {
        var match = TestData.OpenMatch(capacity: 14, externalPlayerCount: 2);

        match.UpdateCapacity(TestData.Organizer, 16, 3, TestData.Now);

        Assert.Equal(16, match.Capacity);
        Assert.Equal(3, match.ExternalPlayerCount);
        Assert.Equal(12, match.MissingPlayers); // 16 - 3 - organizer
    }

    [Fact]
    public void UpdateCapacity_ByNonOrganizer_ThrowsForbidden()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.UpdateCapacity(TestData.Mert, 16, 2, TestData.Now));
        Assert.Equal("match.not_organizer", ex.Code);
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }

    [Fact]
    public void UpdateCapacity_AtKickOff_ThrowsStarted()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.UpdateCapacity(TestData.Organizer, 16, 2, TestData.StartsAt));
        Assert.Equal("match.started", ex.Code);
    }

    [Fact]
    public void UpdateCapacity_OnCancelledMatch_ThrowsNotOpen()
    {
        var match = TestData.OpenMatch();
        match.Cancel(TestData.Organizer, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.UpdateCapacity(TestData.Organizer, 16, 2, TestData.Now));
        Assert.Equal("match.not_open", ex.Code);
    }

    // "Who" comes before "when": a stranger gets 403 even on a cancelled match.
    [Fact]
    public void UpdateCapacity_ByNonOrganizerOnCancelledMatch_ThrowsForbiddenFirst()
    {
        var match = TestData.OpenMatch();
        match.Cancel(TestData.Organizer, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.UpdateCapacity(TestData.Mert, 16, 2, TestData.Now));
        Assert.Equal("match.not_organizer", ex.Code);
    }

    // Organizer + Mert + 2 external = 4 seats taken; 4 is allowed, 2 is not.
    [Fact]
    public void UpdateCapacity_ToExactlyCurrentOccupancy_Succeeds()
    {
        var match = TestData.OpenMatch(capacity: 14, externalPlayerCount: 2);
        match.AddParticipant(TestData.Mert, TestData.Now);

        match.UpdateCapacity(TestData.Organizer, 4, 2, TestData.Now);

        Assert.True(match.IsFull);
    }

    [Fact]
    public void UpdateCapacity_BelowCurrentOccupancy_ThrowsConflictAndKeepsValues()
    {
        var match = TestData.OpenMatch(capacity: 14, externalPlayerCount: 2);
        match.AddParticipant(TestData.Mert, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.UpdateCapacity(TestData.Organizer, 4, 3, TestData.Now));
        Assert.Equal("match.capacity_exceeded", ex.Code);
        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
        Assert.Equal(14, match.Capacity);
        Assert.Equal(2, match.ExternalPlayerCount);
    }

    [Fact]
    public void UpdateCapacity_MixedToOddCapacity_ThrowsAndKeepsValues()
    {
        var match = TestData.OpenMatch(capacity: 14, externalPlayerCount: 2);

        var ex = Assert.Throws<DomainException>(() => match.UpdateCapacity(TestData.Organizer, 15, 2, TestData.Now));
        Assert.Equal("match.mixed_capacity_odd", ex.Code);
        Assert.Equal(14, match.Capacity);
    }

    // ---------- Cancel ----------

    [Fact]
    public void Cancel_ByOrganizerBeforeKickOff_CancelsMatch()
    {
        var match = TestData.OpenMatch();
        var cancelledAt = TestData.StartsAt - TestData.Tick;

        match.Cancel(TestData.Organizer, cancelledAt);

        Assert.Equal(MatchStatus.Cancelled, match.Status);
        Assert.Equal(cancelledAt, match.CancelledAt);
    }

    [Fact]
    public void Cancel_ByNonOrganizer_ThrowsForbiddenAndStaysOpen()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.Cancel(TestData.Mert, TestData.Now));
        Assert.Equal("match.not_organizer", ex.Code);
        Assert.Equal(MatchStatus.Open, match.Status);
        Assert.Null(match.CancelledAt);
    }

    // Once the match has started it can only be completed or end up without result.
    [Fact]
    public void Cancel_AtKickOff_ThrowsStarted()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.Cancel(TestData.Organizer, TestData.StartsAt));
        Assert.Equal("match.started", ex.Code);
        Assert.Equal(MatchStatus.Open, match.Status);
    }

    [Fact]
    public void Cancel_Twice_ThrowsNotOpen()
    {
        var match = TestData.OpenMatch();
        match.Cancel(TestData.Organizer, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.Cancel(TestData.Organizer, TestData.Now));
        Assert.Equal("match.not_open", ex.Code);
    }

    [Fact]
    public void Cancel_CompletedMatch_ThrowsNotOpen()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() => match.Cancel(TestData.Organizer, TestData.CompletedAt));
        Assert.Equal("match.not_open", ex.Code);
        Assert.Equal(MatchStatus.Completed, match.Status);
    }

    // ---------- Complete ----------

    [Fact]
    public void Complete_ByOrganizer_CompletesAndMarksOrganizerPresent()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(TestData.Mert, TestData.Now);

        match.Complete(TestData.Organizer, TestData.CompletedAt);

        Assert.Equal(MatchStatus.Completed, match.Status);
        Assert.Equal(TestData.CompletedAt, match.CompletedAt);
        Assert.Equal(Attendance.Present, match.ParticipationOf(TestData.Organizer)!.Attendance);
        Assert.Equal(Attendance.NotRecorded, match.ParticipationOf(TestData.Mert)!.Attendance); // others untouched
    }

    [Fact]
    public void Complete_ByNonOrganizer_ThrowsForbiddenAndStaysOpen()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(TestData.Mert, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.Complete(TestData.Mert, TestData.CompletedAt));
        Assert.Equal("match.not_organizer", ex.Code);
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
        AssertStillOpen(match);
    }

    [Fact]
    public void Complete_BeforeKickOff_ThrowsNotStarted()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.Complete(TestData.Organizer, TestData.StartsAt - TestData.Tick));
        Assert.Equal("match.not_started", ex.Code);
        AssertStillOpen(match);
    }

    [Fact]
    public void Complete_ExactlyAtKickOff_Succeeds()
    {
        var match = TestData.OpenMatch();

        match.Complete(TestData.Organizer, TestData.StartsAt);

        Assert.Equal(MatchStatus.Completed, match.Status);
    }

    [Fact]
    public void Complete_OnLastMomentOfWindow_Succeeds()
    {
        var match = TestData.OpenMatch();

        match.Complete(TestData.Organizer, CompletionDeadline);

        Assert.Equal(MatchStatus.Completed, match.Status);
    }

    [Fact]
    public void Complete_JustAfterWindow_ThrowsWindowClosed()
    {
        var match = TestData.OpenMatch();

        var ex = Assert.Throws<DomainException>(() => match.Complete(TestData.Organizer, CompletionDeadline + TestData.Tick));
        Assert.Equal("match.completion_window_closed", ex.Code);
        AssertStillOpen(match);
    }

    [Fact]
    public void Complete_CancelledMatch_ThrowsNotOpen()
    {
        var match = TestData.OpenMatch();
        match.Cancel(TestData.Organizer, TestData.Now);

        var ex = Assert.Throws<DomainException>(() => match.Complete(TestData.Organizer, TestData.CompletedAt));
        Assert.Equal("match.not_open", ex.Code);
        Assert.Equal(MatchStatus.Cancelled, match.Status);
        Assert.Null(match.CompletedAt);
    }

    [Fact]
    public void Complete_Twice_ThrowsNotOpenAndKeepsFirstCompletionTime()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() => match.Complete(TestData.Organizer, TestData.CompletedAt.AddHours(1)));
        Assert.Equal("match.not_open", ex.Code);
        Assert.Equal(TestData.CompletedAt, match.CompletedAt);
    }

    // The invariant Complete relies on: the organizer's participation always exists.
    [Fact]
    public void Organizer_AlwaysHasParticipation_EvenAfterOthersLeave()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(TestData.Mert, TestData.Now);
        match.Withdraw(TestData.Mert, TestData.Now);

        Assert.NotNull(match.ParticipationOf(TestData.Organizer));
        Assert.Throws<DomainException>(() => match.Withdraw(TestData.Organizer, TestData.Now));
        Assert.NotNull(match.ParticipationOf(TestData.Organizer));
    }

    // ---------- Roster is frozen after completion ----------

    [Fact]
    public void AddParticipant_AfterCompletion_ThrowsNotOpen()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() => match.AddParticipant(TestData.Emre, TestData.CompletedAt));
        Assert.Equal("match.not_open", ex.Code);
    }

    [Fact]
    public void Withdraw_AfterCompletion_ThrowsNotOpen()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() => match.Withdraw(TestData.Mert, TestData.CompletedAt));
        Assert.Equal("match.not_open", ex.Code);
        Assert.True(match.IsParticipant(TestData.Mert));
    }

    // ---------- IsWithoutResult ----------

    [Fact]
    public void IsWithoutResult_BeforeKickOff_IsFalse()
        => Assert.False(TestData.OpenMatch().IsWithoutResult(TestData.Now));

    [Fact]
    public void IsWithoutResult_OnLastMomentOfCompletionWindow_IsFalse()
        => Assert.False(TestData.OpenMatch().IsWithoutResult(CompletionDeadline));

    [Fact]
    public void IsWithoutResult_JustAfterCompletionWindow_IsTrue()
        => Assert.True(TestData.OpenMatch().IsWithoutResult(CompletionDeadline + TestData.Tick));

    [Fact]
    public void IsWithoutResult_CompletedMatch_IsFalse()
        => Assert.False(TestData.CompletedMatch().IsWithoutResult(CompletionDeadline + TestData.Tick));

    [Fact]
    public void IsWithoutResult_CancelledMatch_IsFalse()
    {
        var match = TestData.OpenMatch();
        match.Cancel(TestData.Organizer, TestData.Now);

        Assert.False(match.IsWithoutResult(CompletionDeadline + TestData.Tick));
    }

    private static void AssertStillOpen(Domain.Entities.Match match)
    {
        Assert.Equal(MatchStatus.Open, match.Status);
        Assert.Null(match.CompletedAt);
        Assert.Equal(Attendance.NotRecorded, match.ParticipationOf(TestData.Organizer)!.Attendance);
    }
}
