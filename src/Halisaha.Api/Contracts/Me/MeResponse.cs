using Halisaha.Application.Queries.GetMe;
using Halisaha.Domain.Enums;

namespace Halisaha.Api.Contracts.Me;

/// <summary>
/// <c>GET /api/me</c> cevabı (L7):
/// <c>{ userId, email, firstName, lastName, phoneNumber, profile: { preferredPosition, skillLevel, district: { id, name },
/// province: { id, name } } | null }</c>. <c>profile</c> her zaman yazılır; oyuncu profili yoksa <c>null</c>.
/// </summary>
public sealed record MeResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    MeProfileResponse? Profile)
{
    public static MeResponse From(MeResult result) => new(
        result.UserId,
        result.Email,
        result.FirstName,
        result.LastName,
        result.PhoneNumber,
        result.Profile is { } profile
            ? new MeProfileResponse(
                profile.PreferredPosition,
                profile.SkillLevel,
                new LocationResponse(profile.District.Id, profile.District.Name),
                new LocationResponse(profile.Province.Id, profile.Province.Name))
            : null);

    // Kişisel veri ToString'de görünmez (LoginRequest ile aynı kalıp).
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append($"UserId = {UserId}, HasProfile = {Profile is not null}");
        return true;
    }
}

public sealed record MeProfileResponse(
    Position PreferredPosition,
    int SkillLevel,
    LocationResponse District,
    LocationResponse Province);

public sealed record LocationResponse(int Id, string Name);
