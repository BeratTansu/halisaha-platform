namespace Halisaha.Domain.Rules;

/// <summary>Business rule limits in one place. Change a rule here, not in the code that uses it.</summary>
public static class MatchRules
{
    /// <summary>After the start time, the organizer has this long to mark the match as completed.</summary>
    public static readonly TimeSpan CompletionWindow = TimeSpan.FromDays(7);

    /// <summary>After completion, attendance can be recorded for this long.</summary>
    public static readonly TimeSpan AttendanceWindow = TimeSpan.FromHours(48);

    /// <summary>After completion, players can rate each other for this long.</summary>
    public static readonly TimeSpan RatingWindow = TimeSpan.FromDays(7);

    public const int MinCapacity = 2;
    public const int MaxCapacity = 30;

    public const int MinScore = 1;
    public const int MaxScore = 5;
    public const int MinSkillLevel = 1;
    public const int MaxSkillLevel = 5;
}