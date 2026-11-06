using Halisaha.Application.Commands.Login;

namespace Halisaha.Api.Contracts.Auth;

/// <summary>
/// <c>POST /api/auth/login</c> gövdesi (L7). Alanlar nullable: isteğin şeklini tek yer (FluentValidation)
/// doğrular, MVC örtük [Required] eklemez. Alan adları <see cref="LoginCommand"/> ile aynı.
/// </summary>
public sealed record LoginRequest(string? Email, string? Password)
{
    public LoginCommand ToCommand() => new(Email ?? string.Empty, Password ?? string.Empty);

    // MVC action argümanlarını Trace'te ToString ile loglar; e-posta da kişisel veridir (RegisterRequest ile aynı).
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Email = ***, Password = ***");
        return true;
    }
}
