namespace Halisaha.Domain.Rules;

/// <summary>Time windows from the domain decisions document, kept in one place.</summary>
public static class MatchRules
{
    /// <summary>A match can be completed at most this long after its start time.</summary>
    public static readonly TimeSpan CompletionWindow = TimeSpan.FromDays(7);

    /// <summary>Attendance can be recorded or edited this long after completion.</summary>
    public static readonly TimeSpan AttendanceWindow = TimeSpan.FromHours(48);

    /// <summary>Ratings are open this long after completion.</summary>
    public static readonly TimeSpan RatingWindow = TimeSpan.FromDays(7);

    public const int MinCapacity = 2;
    public const int MaxCapacity = 30;
    public const int MinScore = 1;
    public const int MaxScore = 5;
}
