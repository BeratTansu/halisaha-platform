using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Entities;

/// <summary>
/// The current roster: the single source of truth for accepted members.
/// A player who withdraws is removed from the roster; the history stays in <see cref="MatchRequest"/>.
/// </summary>
public sealed class MatchParticipation
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid PlayerId { get; private set; }
    public Team? Team { get; private set; }
    public Attendance Attendance { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    private MatchParticipation() { }

    internal MatchParticipation(Guid matchId, Guid playerId, DateTimeOffset joinedAt)
    {
        Id = Guid.NewGuid();
        MatchId = matchId;
        PlayerId = playerId;
        Attendance = Attendance.NotRecorded;
        JoinedAt = joinedAt;
    }

    internal void SetAttendance(Attendance attendance) => Attendance = attendance;
    internal void SetTeam(Team? team) => Team = team;
}
