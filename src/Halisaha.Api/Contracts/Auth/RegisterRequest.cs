using Halisaha.Application.Commands.Register;
using Halisaha.Domain.Enums;

namespace Halisaha.Api.Contracts.Auth;

/// <summary>
/// <c>POST /api/auth/register</c> gövdesi: HTTP sözleşmesi, use case modelinden ayrı. Alan adları
/// <see cref="RegisterCommand"/> ile birebir aynıdır; validator hataları command'ın alan adlarıyla gelir ve
/// istemcinin gönderdiği alanlarla eşleşmelidir.
/// Metin alanları nullable: HTTP'den her şey eksik gelebilir. Bu, MVC'nin non-nullable alanlara örtük
/// [Required] eklemesini de engeller; böylece isteğin şeklini tek bir yer (FluentValidation) doğrular.
/// </summary>
public sealed record RegisterRequest(
    string? Email,
    string? Password,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    Position PreferredPosition,
    int SkillLevel,
    int PreferredDistrictId)
{
    // Command constructor parametreli: command'a eklenen yeni bir alan burada derleme hatası verir.
    // Eksik metinler boş string olur; validator "boş olamaz" der.
    public RegisterCommand ToCommand() => new(
        Email ?? string.Empty,
        Password ?? string.Empty,
        FirstName ?? string.Empty,
        LastName ?? string.Empty,
        PhoneNumber,
        PreferredPosition,
        SkillLevel,
        PreferredDistrictId);

    // MVC action argümanlarını Trace seviyesinde ToString ile loglar (ControllerActionInvoker; 6 Kasım'da
    // LoginEndpointTests'teki log testiyle görüldü). Bu yüzden parola gibi kişisel veriler de maskelenir: log'a
    // kişisel veri yazılmaz. Yalnızca kişisel olmayan alanlar görünür.
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Email = ***, Password = ***, FirstName = ***, LastName = ***, PhoneNumber = ***, ");
        builder.Append($"PreferredPosition = {PreferredPosition}, ");
        builder.Append($"SkillLevel = {SkillLevel}, PreferredDistrictId = {PreferredDistrictId}");
        return true;
    }
}
