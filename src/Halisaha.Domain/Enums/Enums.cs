namespace Halisaha.Domain.Enums;

public enum Position { Goalkeeper = 1, Defender = 2, Midfielder = 3, Forward = 4 }

/// <summary>Mixed: the whole match is in the organizer's scope. WithOpponent: only the organizer's side.</summary>
public enum MatchType { Mixed = 1, WithOpponent = 2 }

/// <summary>Only these states are stored. "Full", "started" and "without result" are computed.</summary>
public enum MatchStatus { Open = 1, Completed = 2, Cancelled = 3 }

public enum RequestType { Application = 1, Invitation = 2 }

public enum RequestStatus { Pending = 1, Accepted = 2, Rejected = 3, Withdrawn = 4 }

public enum Team { A = 1, B = 2 }

public enum Attendance { NotRecorded = 0, Present = 1, Absent = 2 }
