using System.Text;
using Halisaha.Domain.Enums;

namespace Halisaha.Application.Queries.GetMe;

/// <summary>
/// <c>GET /api/me</c>'nin verisi (L7). Bir okuma modeli: domain entity'si değil, sorgunun doğrudan ürettiği DTO
/// (Karar 6). <see cref="Profile"/> null ise kullanıcının oyuncu profili yoktur (ör. Admin, Karar 2 bileşimi).
/// </summary>
public sealed record MeResult(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    PlayerProfileSummary? Profile)
{
    // Kişisel veri ToString'de görünmez (log'a kişisel veri yazılmaz).
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"UserId = {UserId}, HasProfile = {Profile is not null}");
        return true;
    }
}

public sealed record PlayerProfileSummary(
    Position PreferredPosition,
    int SkillLevel,
    LocationSummary District,
    LocationSummary Province);

public sealed record LocationSummary(int Id, string Name);
