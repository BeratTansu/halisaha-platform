using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;

namespace Halisaha.Domain.Entities;

public class PlayerProfile
{
    public Guid UserId { get; private set; }
    public Position PreferredPosition { get; private set; } 
    public int SkillLevel { get; private set; }
    public int PreferredDistrictId { get; private set; }
    public District? PreferredDistrict { get; private set; }

    private PlayerProfile() { } // EF Core

    public PlayerProfile(Guid userId, Position position, int skillLevel, int districtId)
    {
        if (userId == Guid.Empty)
        {
            throw DomainException.Validation("profile.user_required", "User Id is required.");
        }
        UserId = userId;

        Update(position, skillLevel, districtId);
    }

    public void Update(Position position, int skillLevel, int districtId)
    {
        if (!Enum.IsDefined(position))
        {
            throw DomainException.Validation("profile.invalid_position", "Position must be one of the defined values.");
        }

        if (skillLevel is < MatchRules.MinSkillLevel or > MatchRules.MaxSkillLevel)
        {
            throw DomainException.Validation("profile.invalid_skill_level", $"Skill level must be between {MatchRules.MinSkillLevel} and {MatchRules.MaxSkillLevel}.");
        }

        PreferredPosition = position;
        SkillLevel = skillLevel;
        PreferredDistrictId = districtId;
    }
}