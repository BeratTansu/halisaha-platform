using FluentValidation;

namespace Halisaha.Application.Commands.Register;

/// <summary>
/// Yalnızca isteğin şekli. Normalize edilmiş command üzerinde çalışır (handler önce normalize eder).
/// Bilerek burada olmayanlar:
/// - Parola politikası (uzunluk, rakam, küçük harf): tek yeri Identity ayarları (C3); kopyası iki kaynak olurdu.
/// - Seviye aralığı ve pozisyonun geçerliliği: domain'in kuralı (PlayerProfile).
/// - İlçenin varlığı: veritabanı gerektirir, handler kontrol eder.
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public const int MaxNameLength = 50;

    // Identity şemasında Email kolonu varchar(256). Daha uzun bir değer DB'de 22001 hatası (500) verirdi.
    public const int MaxEmailLength = 256;

    public const int MaxPhoneNumberLength = 20;

    // Rakam ve boşluk; '+' yalnızca en başta ve tek; en az bir rakam. E.164 normalizasyonu yapılmaz.
    private const string PhoneNumberPattern = @"^\+?[0-9 ]*[0-9][0-9 ]*$";

    public RegisterCommandValidator()
    {
        // Her alan için ilk hatada dur: boş e-posta hem "boş olamaz" hem "geçersiz e-posta" demesin.
        // Alanlar arası değil, alan içi: farklı alanların hataları yine birlikte döner.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Email)
            .NotEmpty()
            .MaximumLength(MaxEmailLength)
            .EmailAddress();

        // Yalnızca "gönderildi mi". Politika Identity'de; boş parola Identity'ye hiç gitmesin diye burada.
        RuleFor(c => c.Password)
            .NotEmpty();

        RuleFor(c => c.FirstName)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(c => c.LastName)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(c => c.PhoneNumber)
            .MaximumLength(MaxPhoneNumberLength)
            .Matches(PhoneNumberPattern)
            .When(c => c.PhoneNumber is not null);
    }
}
