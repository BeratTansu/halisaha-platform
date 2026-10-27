using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using static Halisaha.Domain.Tests.TestData;

namespace Halisaha.Domain.Tests;

public class MatchLifecycleTests
{
    [Fact]
    public void Cannot_complete_before_start()
    {
        var match = OpenMatch();
        var ex = Assert.Throws<DomainException>(() => match.Complete(Organizer, StartsAt.AddSeconds(-1)));
        Assert.Equal("match.not_started", ex.Code);
    }

    [Fact]
    public void Can_complete_exactly_at_start_and_on_day_7()
    {
        OpenMatch().Complete(Organizer, StartsAt);
        var match = OpenMatch();
        match.Complete(Organizer, StartsAt + MatchRules.CompletionWindow);
        Assert.Equal(MatchStatus.Completed, match.Status);
    }

    [Fact]
    public void Cannot_complete_after_day_7()
    {
        var match = OpenMatch();
        var ex = Assert.Throws<DomainException>(() =>
            match.Complete(Organizer, StartsAt + MatchRules.CompletionWindow + TimeSpan.FromSeconds(1)));
        Assert.Equal("match.completion_window_closed", ex.Code);
    }

    [Fact]
    public void Match_still_open_after_day_7_is_without_result()
    {
        var match = OpenMatch();
        Assert.False(match.IsWithoutResult(StartsAt + MatchRules.CompletionWindow));
        Assert.True(match.IsWithoutResult(StartsAt + MatchRules.CompletionWindow + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Only_organizer_can_complete()
    {
        var match = OpenMatch();
        var ex = Assert.Throws<DomainException>(() => match.Complete(NewPlayer(), StartsAt));
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
    }

    [Fact]
    public void Completed_and_cancelled_are_final()
    {
        var completed = OpenMatch();
        completed.Complete(Organizer, StartsAt);
        Assert.Throws<DomainException>(() => completed.Cancel(Organizer, StartsAt));

        var cancelled = OpenMatch();
        cancelled.Cancel(Organizer, Now);
        Assert.Throws<DomainException>(() => cancelled.Complete(Organizer, StartsAt));
        Assert.Throws<DomainException>(() => cancelled.AddParticipant(NewPlayer(), Now));
    }

    [Fact]
    public void Attendance_can_be_edited_until_48_hours_after_completion()
    {
        var player = NewPlayer();
        var match = CompletedMatch(StartsAt, player);
        match.RecordAttendance(Organizer, player, Attendance.Present, StartsAt + MatchRules.AttendanceWindow);
        Assert.Equal(Attendance.Present, match.ParticipationOf(player)!.Attendance);

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(Organizer, player, Attendance.Absent, StartsAt + MatchRules.AttendanceWindow + TimeSpan.FromSeconds(1)));
        Assert.Equal("attendance.window_closed", ex.Code);
    }

    [Fact]
    public void Attendance_requires_a_completed_match()
    {
        var player = NewPlayer();
        var match = OpenMatch();
        match.AddParticipant(player, Now);
        var ex = Assert.Throws<DomainException>(() => match.RecordAttendance(Organizer, player, Attendance.Present, StartsAt));
        Assert.Equal("attendance.match_not_completed", ex.Code);
    }

    [Fact]
    public void Organizer_starts_as_not_recorded()
    {
        var match = CompletedMatch(StartsAt);
        Assert.Equal(Attendance.NotRecorded, match.ParticipationOf(Organizer)!.Attendance);
    }
}
