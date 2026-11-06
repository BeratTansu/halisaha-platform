using System.Text;
using Halisaha.Domain.Enums;

namespace Halisaha.Application.Commands.Register;

/// <summary>
/// Register use case'inin girdisi. Constructor parametreli record: command'a yeni bir alan eklenince API'deki
/// mapping derleme hatası verir, alan sessizce kaybolmaz. Alan adları RegisterRequest (API) ile aynıdır;
/// validator hatalarının alan adları buradan gelir ve istemcinin gönderdiği alanlarla eşleşmelidir.
/// </summary>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Position PreferredPosition,
    int SkillLevel,
    int PreferredDistrictId)
{
    /// <summary>
    /// Doğrulanacak ve saklanacak hali: metinler kırpılır, boş telefon null olur. Parolaya dokunulmaz (boşluk
    /// parolanın parçası olabilir). JSON'da gelmeyen bir metin alanı (runtime'da null) boş string'e çevrilir;
    /// böylece validator onu "boş" olarak raporlar ve handler NullReferenceException görmez.
    /// </summary>
    public RegisterCommand Normalize() => this with
    {
        Email = TrimOrEmpty(Email),
        Password = Password ?? string.Empty,
        FirstName = TrimOrEmpty(FirstName),
        LastName = TrimOrEmpty(LastName),
        PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
    };

    // Record'un otomatik ToString'i bütün alanları yazar; command bir gün loglanırsa parola log'a düşerdi.
    // Bu metot otomatik üretilen PrintMembers'ın yerine geçer. Yeni bir alan eklenirse buraya da eklenmeli;
    // unutulursa yalnızca ToString'de görünmez (güvenli taraf).
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Email = {Email}, Password = ***, FirstName = {FirstName}, LastName = {LastName}, ");
        builder.Append($"PhoneNumber = {PhoneNumber}, PreferredPosition = {PreferredPosition}, ");
        builder.Append($"SkillLevel = {SkillLevel}, PreferredDistrictId = {PreferredDistrictId}");
        return true;
    }

    private static string TrimOrEmpty(string? value) => value?.Trim() ?? string.Empty;
}
