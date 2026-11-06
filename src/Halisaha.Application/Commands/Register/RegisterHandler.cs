using FluentValidation;
using FluentValidation.Results;
using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Application.Errors;
using Halisaha.Domain.Entities;

namespace Halisaha.Application.Commands.Register;

/// <summary>
/// Register use case'i: kullanıcı (Identity) + oyuncu profili, tek transaction'da. Yalnızca Application port'larını
/// ve Domain'i bilir. Sıranın her adımı bir kurala dayanır; adımların yerini değiştirmek bir kuralı bozar.
/// </summary>
public sealed class RegisterHandler(
    IValidator<RegisterCommand> validator,
    ILocationQueries locations,
    IIdentityService identity,
    IPlayerProfileRepository profiles,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<RegisterResult> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Önce normalize, sonra doğrula: doğrulanan değer saklanacak değerdir.
        var normalized = command.Normalize();

        // 2. İsteğin şekli (400). Hata önceliği: Shape önce gelir.
        await validator.ValidateAndThrowAsync(normalized, cancellationToken);

        // 3. İlçe var mı: transaction DIŞINDA. Transaction gereksiz yere açılmaz; ilçeler silinmediği için
        //    kontrol ile insert arasında yarış yoktur. Böylece 23503 normal akışta oluşmaz.
        if (!await locations.DistrictExistsAsync(normalized.PreferredDistrictId, cancellationToken))
        {
            throw ProfileErrors.DistrictNotFound();
        }

        // 4-5. Id'yi handler üretir; aynı Id Identity kullanıcısına (Id ve UserName) ve profile gider.
        //      Profil, parola hash'lenmeden ve DB'ye yazılmadan önce oluşturulur: domain kuralları (seviye,
        //      pozisyon) bedava yerde reddedilir.
        var userId = Guid.NewGuid();
        var profile = new PlayerProfile(
            userId, normalized.PreferredPosition, normalized.SkillLevel, normalized.PreferredDistrictId);

        var newUser = new NewIdentityUser(
            userId,
            normalized.Email,
            normalized.FirstName,
            normalized.LastName,
            normalized.PhoneNumber,
            timeProvider.GetUtcNow());

        // 6. Tek transaction: Identity'nin kendi SaveChanges'i de bu transaction'ın içinde kalır; profil
        //    eklendikten sonra UoW kaydeder ve commit eder. Herhangi bir hata ikisini birlikte geri alır.
        //    Yarışta 23505 (EmailIndex) UoW'de auth.email_taken'a çevrilir.
        await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            var result = await identity.CreateUserAsync(newUser, normalized.Password);

            switch (result.Status)
            {
                case IdentityCreationStatus.Succeeded:
                    break;
                case IdentityCreationStatus.EmailTaken:
                    throw AuthErrors.EmailTaken();                          // 409
                case IdentityCreationStatus.Rejected:
                    throw ToValidationException(result.Errors);             // 400, validator ile aynı biçim
                default:
                    throw new InvalidOperationException($"Unknown identity creation status '{result.Status}'.");
            }

            profiles.Add(profile);
        }, cancellationToken);

        // 7. Login diliminde: token burada, commit'ten SONRA ve transaction dışında üretilecek (ITokenService).
        return new RegisterResult(userId);
    }

    // Identity'nin reddi, validator'ın hatalarıyla aynı biçimde (ValidationException, command'ın alan adları)
    // döner; istemci hatanın hangi katmandan geldiğini bilmez.
    private static ValidationException ToValidationException(IReadOnlyList<IdentityFieldError> errors) =>
        new(errors.Select(e => new ValidationFailure(FieldName(e.Field), e.Message)));

    private static string FieldName(IdentityField field) => field switch
    {
        IdentityField.Email => nameof(RegisterCommand.Email),
        IdentityField.Password => nameof(RegisterCommand.Password),
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown identity field."),
    };
}
