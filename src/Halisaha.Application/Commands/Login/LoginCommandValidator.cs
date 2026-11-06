using FluentValidation;

namespace Halisaha.Application.Commands.Login;

/// <summary>
/// L3: yalnızca "gönderildi mi". Bilerek burada olmayanlar:
/// - E-posta biçimi: biçimi bozuk bir e-postayla zaten hesap yoktur; sonuç 401 olur.
/// - Parola politikası: politika değişirse eski parolası politikaya uymayan kullanıcı içeri giremezdi.
/// - Uzunluk sınırı: login hiçbir şey yazmaz; uzun e-posta yalnızca bulunamaz.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty();
        RuleFor(c => c.Password).NotEmpty();
    }
}
