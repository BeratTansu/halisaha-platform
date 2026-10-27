using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Entities;

/// <summary>
/// Player data, kept apart from the identity user (composition, not inheritance).
/// Its primary key is the user's id, so organizer and participant ids are always the same value.
/// </summary>
public sealed class PlayerProfile
{
    public Guid UserId { get; private set; }
    public Position PreferredPosition { get; private set; }

    /// <summary>Self-declared level, 1-5. The rating average is computed separately.</summary>
    public int SkillLevel { get; private set; }

    public int PreferredDistrictId { get; private set; }
    public District? PreferredDistrict { get; private set; }

    private PlayerProfile() { }

    public PlayerProfile(Guid userId, Position preferredPosition, int skillLevel, int preferredDistrictId)
    {
        if (userId == Guid.Empty)
            throw DomainException.Validation("profile.user_required", "A profile needs a user.");
        UserId = userId;
        Update(preferredPosition, skillLevel, preferredDistrictId);
    }

    public void Update(Position preferredPosition, int skillLevel, int preferredDistrictId)
    {
        if (!Enum.IsDefined(preferredPosition))
            throw DomainException.Validation("profile.invalid_position", "Unknown position.");
        if (skillLevel is < 1 or > 5)
            throw DomainException.Validation("profile.invalid_skill_level", "Skill level must be between 1 and 5.");

        PreferredPosition = preferredPosition;
        SkillLevel = skillLevel;
        PreferredDistrictId = preferredDistrictId;
    }
}
