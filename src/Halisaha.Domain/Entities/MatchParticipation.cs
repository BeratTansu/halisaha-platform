using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Entities;

public class MatchParticipation
{
    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid PlayerId { get; private set; }
    public Team? Team { get; private set; }
    public Attendance Attendance { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private MatchParticipation() { } // EF Core

    internal MatchParticipation(Guid matchId, Guid playerId, DateTime now)
    {
        Id = Guid.NewGuid();
        MatchId = matchId;
        PlayerId = playerId;
        JoinedAt = now;
        Attendance = Attendance.NotRecorded;
    }

    internal void SetTeam(Team team) => Team = team;
    internal void SetAttendance(Attendance attendance) => Attendance = attendance;
}