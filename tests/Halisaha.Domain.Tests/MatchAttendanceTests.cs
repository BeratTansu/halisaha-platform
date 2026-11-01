using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;

namespace Halisaha.Domain.Tests;

public class MatchAttendanceTests
{
    private static readonly DateTimeOffset AttendanceDeadline = TestData.CompletedAt + MatchRules.AttendanceWindow;
    private static readonly DateTimeOffset RatingDeadline = TestData.CompletedAt + MatchRules.RatingWindow;

    // ---------- Recording ----------

    [Theory]
    [InlineData(Attendance.Present)]
    [InlineData(Attendance.Absent)]
    public void RecordAttendance_ByOrganizer_SetsValue(Attendance attendance)
    {
        var match = TestData.CompletedMatch();

        match.RecordAttendance(TestData.Organizer, TestData.Mert, attendance, TestData.CompletedAt);

        Assert.Equal(attendance, match.ParticipationOf(TestData.Mert)!.Attendance);
    }

    // A mistake can be corrected while the window is open; the value at window close is what counts.
    [Fact]
    public void RecordAttendance_WithinWindow_CanBeCorrected()
    {
        var match = TestData.CompletedMatch();
        match.RecordAttendance(TestData.Organizer, TestData.Mert, Attendance.Absent, TestData.CompletedAt);

        match.RecordAttendance(TestData.Organizer, TestData.Mert, Attendance.Present, TestData.CompletedAt.AddHours(5));

        Assert.Equal(Attendance.Present, match.ParticipationOf(TestData.Mert)!.Attendance);
    }

    [Fact]
    public void UnrecordedPlayer_AfterCompletion_StaysNotRecorded()
        => Assert.Equal(Attendance.NotRecorded, TestData.CompletedMatch().ParticipationOf(TestData.Mert)!.Attendance);

    // ---------- Window ----------

    [Fact]
    public void RecordAttendance_OnLastMomentOfWindow_Succeeds()
    {
        var match = TestData.CompletedMatch();

        match.RecordAttendance(TestData.Organizer, TestData.Mert, Attendance.Present, AttendanceDeadline);

        Assert.Equal(Attendance.Present, match.ParticipationOf(TestData.Mert)!.Attendance);
    }

    [Fact]
    public void RecordAttendance_JustAfterWindow_ThrowsAndKeepsValue()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(TestData.Organizer, TestData.Mert, Attendance.Present, AttendanceDeadline + TestData.Tick));
        Assert.Equal("attendance.window_closed", ex.Code);
        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
        Assert.Equal(Attendance.NotRecorded, match.ParticipationOf(TestData.Mert)!.Attendance);
    }

    [Fact]
    public void RecordAttendance_OnOpenMatch_ThrowsNotCompleted()
    {
        var match = TestData.OpenMatch();
        match.AddParticipant(TestData.Mert, TestData.Now);

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(TestData.Organizer, TestData.Mert, Attendance.Present, TestData.CompletedAt));
        Assert.Equal("attendance.match_not_completed", ex.Code);
    }

    // ---------- Who and whom ----------

    [Fact]
    public void RecordAttendance_ByNonOrganizer_ThrowsForbiddenAndKeepsValue()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(TestData.Mert, TestData.Mert, Attendance.Present, TestData.CompletedAt));
        Assert.Equal("match.not_organizer", ex.Code);
        Assert.Equal(DomainErrorKind.Forbidden, ex.Kind);
        Assert.Equal(Attendance.NotRecorded, match.ParticipationOf(TestData.Mert)!.Attendance);
    }

    [Fact]
    public void RecordAttendance_ForOrganizer_ThrowsAndOrganizerStaysPresent()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(TestData.Organizer, TestData.Organizer, Attendance.Absent, TestData.CompletedAt));
        Assert.Equal("attendance.cannot_record_organizer", ex.Code);
        Assert.Equal(DomainErrorKind.Validation, ex.Kind);
        Assert.Equal(Attendance.Present, match.ParticipationOf(TestData.Organizer)!.Attendance);
    }

    [Fact]
    public void RecordAttendance_ForNonParticipant_ThrowsNotParticipant()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(TestData.Organizer, TestData.Emre, Attendance.Present, TestData.CompletedAt));
        Assert.Equal("match.not_participant", ex.Code);
    }

    // ---------- Shape (checked before who/when) ----------

    [Fact]
    public void RecordAttendance_WithEmptyPlayer_ThrowsPlayerRequired()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(TestData.Organizer, Guid.Empty, Attendance.Present, TestData.CompletedAt));
        Assert.Equal("match.player_required", ex.Code);
    }

    [Theory]
    [InlineData(Attendance.NotRecorded)]
    [InlineData((Attendance)99)]
    public void RecordAttendance_WithInvalidValue_Throws(Attendance attendance)
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(TestData.Organizer, TestData.Mert, attendance, TestData.CompletedAt));
        Assert.Equal("attendance.invalid_value", ex.Code);
    }

    // Shape beats who: a stranger sending NotRecorded gets 400, not 403.
    [Fact]
    public void RecordAttendance_InvalidValueByNonOrganizer_ThrowsValidationFirst()
    {
        var match = TestData.CompletedMatch();

        var ex = Assert.Throws<DomainException>(() =>
            match.RecordAttendance(TestData.Mert, TestData.Mert, Attendance.NotRecorded, TestData.CompletedAt));
        Assert.Equal("attendance.invalid_value", ex.Code);
    }

    // ---------- IsAttendanceOpen / IsRatingOpen ----------

    [Fact]
    public void IsAttendanceOpen_AtCompletion_IsTrue()
        => Assert.True(TestData.CompletedMatch().IsAttendanceOpen(TestData.CompletedAt));

    [Fact]
    public void IsAttendanceOpen_OnLastMoment_IsTrue()
        => Assert.True(TestData.CompletedMatch().IsAttendanceOpen(AttendanceDeadline));

    [Fact]
    public void IsAttendanceOpen_JustAfter_IsFalse()
        => Assert.False(TestData.CompletedMatch().IsAttendanceOpen(AttendanceDeadline + TestData.Tick));

    [Fact]
    public void IsAttendanceOpen_BeforeCompletion_IsFalse()
        => Assert.False(TestData.CompletedMatch().IsAttendanceOpen(TestData.CompletedAt - TestData.Tick));

    [Fact]
    public void IsAttendanceOpen_OpenMatch_IsFalse()
        => Assert.False(TestData.OpenMatch().IsAttendanceOpen(TestData.CompletedAt));

    [Fact]
    public void IsRatingOpen_AtCompletion_IsTrue()
        => Assert.True(TestData.CompletedMatch().IsRatingOpen(TestData.CompletedAt));

    [Fact]
    public void IsRatingOpen_OnLastMoment_IsTrue()
        => Assert.True(TestData.CompletedMatch().IsRatingOpen(RatingDeadline));

    [Fact]
    public void IsRatingOpen_JustAfter_IsFalse()
        => Assert.False(TestData.CompletedMatch().IsRatingOpen(RatingDeadline + TestData.Tick));

    [Fact]
    public void IsRatingOpen_BeforeCompletion_IsFalse()
        => Assert.False(TestData.CompletedMatch().IsRatingOpen(TestData.CompletedAt - TestData.Tick));

    [Fact]
    public void IsRatingOpen_OpenMatch_IsFalse()
        => Assert.False(TestData.OpenMatch().IsRatingOpen(TestData.CompletedAt));
}
