using System.Text;

namespace Halisaha.Application.Commands.Login;

/// <summary>
/// Login use case'inin girdisi. Alan adları LoginRequest (API) ile aynıdır; validator hatalarının alan adları
/// buradan gelir.
/// </summary>
public sealed record LoginCommand(string Email, string Password)
{
    /// <summary>
    /// RegisterCommand ile aynı kural: e-posta kırpılır, parolaya dokunulmaz. Kayıtta kırpılıp saklanan e-posta
    /// girişte kırpılmasaydı " ahmet@x.com" ile giriş yapılamazdı. JSON'da gelmeyen alan boş string olur.
    /// </summary>
    public LoginCommand Normalize() => this with
    {
        Email = Email?.Trim() ?? string.Empty,
        Password = Password ?? string.Empty,
    };

    // Otomatik ToString parolayı yazardı (RegisterCommand ile aynı kalıp).
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Email = {Email}, Password = ***");
        return true;
    }
}
