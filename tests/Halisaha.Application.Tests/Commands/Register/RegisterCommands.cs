using Halisaha.Application.Commands.Register;
using Halisaha.Domain.Enums;

namespace Halisaha.Application.Tests.Commands.Register;

/// <summary>Geçerli bir Register command'ı; testler yalnızca konu ettikleri alanı değiştirir (with).</summary>
public static class RegisterCommands
{
    public const string Password = "halisaha1";

    public static RegisterCommand Valid() => new(
        Email: "ahmet@example.com",
        Password: Password,
        FirstName: "Ahmet",
        LastName: "Yılmaz",
        PhoneNumber: "+90 555 123 45 67",
        PreferredPosition: Position.Goalkeeper,
        SkillLevel: 3,
        PreferredDistrictId: 34015);
}
