using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Halisaha.Domain.Tests;

public class PlayerProfileTests
{
    [Fact]
    public void Create_WithValidData_SetsAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var profile = new PlayerProfile(userId, Position.Goalkeeper, 3, 34001);

        // Assert
        Assert.Equal(userId, profile.UserId);
        Assert.Equal(Position.Goalkeeper, profile.PreferredPosition);
        Assert.Equal(3, profile.SkillLevel);
        Assert.Equal(34001, profile.PreferredDistrictId);
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        // Arrange
        var userId = Guid.Empty;

        // Act + Assert
        var ex = Assert.Throws<DomainException>(() => new PlayerProfile(userId, Position.Goalkeeper, 3, 34001));
        Assert.Equal("profile.user_required", ex.Code);
    }

    [Theory]
    [InlineData(MatchRules.MinSkillLevel - 1)]
    [InlineData(MatchRules.MaxSkillLevel + 1)]
    public void Create_WithSkillLevelOutOfRange_Throws(int skillLevel)
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act + Assert
        var ex = Assert.Throws<DomainException>(() => new PlayerProfile(userId, Position.Goalkeeper, skillLevel, 34001));
        Assert.Equal("profile.invalid_skill_level", ex.Code);
    }

    [Theory]
    [InlineData(MatchRules.MinSkillLevel)]
    [InlineData(MatchRules.MaxSkillLevel)]
    public void Create_WithSkillLevelInRange_SetsSkillLevel(int skillLevel)
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var profile = new PlayerProfile(userId, Position.Goalkeeper, skillLevel, 34001);

        // Assert
        Assert.Equal(skillLevel, profile.SkillLevel);
    }

    // 0 and 99 have no label in the Position enum, so both must be rejected.
    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Create_WithUndefinedPosition_Throws(int position)
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act + Assert
        var ex = Assert.Throws<DomainException>(() => new PlayerProfile(userId, (Position)position, 3, 34001));
        Assert.Equal("profile.invalid_position", ex.Code);
    }

    // A failed update must not leave the profile half-changed ("check first, then change").
    [Fact]
    public void Update_WithInvalidSkillLevel_LeavesProfileUnchanged()
    {
        // Arrange
        var profile = new PlayerProfile(Guid.NewGuid(), Position.Goalkeeper, 3, 34001);

        // Act
        Assert.Throws<DomainException>(() => profile.Update(Position.Forward, MatchRules.MaxSkillLevel + 1, 6001));

        // Assert
        Assert.Equal(Position.Goalkeeper, profile.PreferredPosition);
        Assert.Equal(3, profile.SkillLevel);
        Assert.Equal(34001, profile.PreferredDistrictId);
    }
}