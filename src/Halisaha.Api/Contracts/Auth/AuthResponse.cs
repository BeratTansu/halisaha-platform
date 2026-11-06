using Halisaha.Application.Commands;

namespace Halisaha.Api.Contracts.Auth;

/// <summary>
/// Kimliği doğrulanmış kullanıcıya dönen ortak cevap (L7): <c>{ userId, accessToken, expiresIn }</c>.
/// <c>expiresIn</c> saniye cinsinden ve görecedir: istemci kendi saatine ekler, sunucuyla saat farkından
/// etkilenmez. Login bunu döner; Register otomatik giriş diliminde aynı gövdeye geçer.
/// </summary>
public sealed record AuthResponse(Guid UserId, string AccessToken, int ExpiresIn)
{
    public static AuthResponse From(AuthResult result) => new(
        result.UserId,
        result.AccessToken.Value,
        (int)result.AccessToken.ExpiresIn.TotalSeconds);

    // Cevap loglanırsa geçerli bir token log'a düşmesin (AccessToken ile aynı kalıp).
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append($"UserId = {UserId}, AccessToken = ***, ExpiresIn = {ExpiresIn}");
        return true;
    }
}
