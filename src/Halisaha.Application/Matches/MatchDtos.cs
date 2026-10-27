using FluentValidation;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using MatchType = Halisaha.Domain.Enums.MatchType;

namespace Halisaha.Application.Matches;

public sealed record CreateMatchRequest(
    int VenueId, MatchType Type, DateTimeOffset StartsAt, int Capacity, int ExternalPlayerCount, string? Description);

public sealed record UpdateCapacityRequest(int Capacity, int ExternalPlayerCount);

public sealed record MatchSearchQuery(
    int? ProvinceId, int? DistrictId, double? Latitude, double? Longitude, bool OnlyWithFreeSlots = true);

public sealed record MatchSummaryResponse(
    Guid Id, MatchType Type, DateTimeOffset StartsAt, int Capacity, int MissingPlayers,
    MatchStatus Status, string StatusLabel, int VenueId, string VenueName, string DistrictName, double? DistanceKm);

public sealed record ParticipantResponse(Guid PlayerId, string FullName, Position PreferredPosition, bool IsOrganizer, Team? Team, Attendance Attendance);

public sealed record MatchDetailResponse(
    Guid Id, MatchType Type, DateTimeOffset StartsAt, int Capacity, int ExternalPlayerCount, int MissingPlayers,
    MatchStatus Status, string StatusLabel, string? Description, Guid OrganizerUserId, string OrganizerName,
    int VenueId, string VenueName, string VenueAddress, double VenueLatitude, double VenueLongitude,
    string DistrictName, DateTimeOffset? CompletedAt, IReadOnlyList<ParticipantResponse> Participants);

public sealed record MatchRequestResponse(
    Guid Id, Guid MatchId, Guid PlayerId, string PlayerName, RequestType Type, RequestStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset? RespondedAt);

public sealed record InvitePlayerRequest(Guid PlayerId);

public sealed record AttendanceItem(Guid PlayerId, Attendance Attendance);
public sealed record RecordAttendanceRequest(IReadOnlyList<AttendanceItem> Items);

public sealed record RateRequest(Guid RateeId, int Score);

public sealed class CreateMatchRequestValidator : AbstractValidator<CreateMatchRequest>
{
    public CreateMatchRequestValidator()
    {
        RuleFor(x => x.VenueId).GreaterThan(0);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Capacity).InclusiveBetween(MatchRules.MinCapacity, MatchRules.MaxCapacity);
        RuleFor(x => x.Capacity).Must(c => c % 2 == 0).When(x => x.Type == MatchType.Mixed)
            .WithMessage("A mixed match needs an even capacity.");
        RuleFor(x => x.ExternalPlayerCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateCapacityRequestValidator : AbstractValidator<UpdateCapacityRequest>
{
    public UpdateCapacityRequestValidator()
    {
        RuleFor(x => x.Capacity).InclusiveBetween(MatchRules.MinCapacity, MatchRules.MaxCapacity);
        RuleFor(x => x.ExternalPlayerCount).GreaterThanOrEqualTo(0);
    }
}

public sealed class RecordAttendanceRequestValidator : AbstractValidator<RecordAttendanceRequest>
{
    public RecordAttendanceRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(x => x.Attendance).IsInEnum());
        RuleFor(x => x.Items).Must(items => items.Select(i => i.PlayerId).Distinct().Count() == items.Count)
            .WithMessage("Each player can appear only once.");
    }
}

public sealed class RateRequestValidator : AbstractValidator<RateRequest>
{
    public RateRequestValidator()
    {
        RuleFor(x => x.RateeId).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(MatchRules.MinScore, MatchRules.MaxScore);
    }
}

internal static class StatusLabels
{
    /// <summary>The stored status plus the computed states the UI needs ("full", "started", "without result").</summary>
    public static string For(MatchStatus status, DateTimeOffset startsAt, int missing, DateTimeOffset now)
    {
        if (status != MatchStatus.Open) return status.ToString();
        if (now > startsAt + MatchRules.CompletionWindow) return "WithoutResult";
        if (now >= startsAt) return "Started";
        return missing <= 0 ? "Full" : "Open";
    }
}
