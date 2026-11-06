using System.Text;

namespace Halisaha.Application.Abstractions.Identity;

/// <summary>
/// Üretilmiş bir access token ve geçerlilik süresi. Süre görecedir (L7: istemci <c>expiresIn</c> alır, kendi
/// saatine ekler); saniyeye çevirmek API sözleşmesinin işidir.
/// </summary>
public sealed record AccessToken(string Value, TimeSpan ExpiresIn)
{
    // Record'un otomatik ToString'i token'ı yazardı; sonuç bir gün loglanırsa geçerli bir token log'a düşerdi.
    // RegisterCommand'daki parola maskelemesiyle aynı kalıp.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Value = ***, ExpiresIn = {ExpiresIn}");
        return true;
    }
}
