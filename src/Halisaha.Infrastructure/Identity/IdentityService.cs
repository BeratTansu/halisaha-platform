using Halisaha.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace Halisaha.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityService"/>'in ASP.NET Identity adapter'ı. UserManager'ı kullanır ve onun sonucunu
/// (IdentityResult) Application'ın sonuç tipine çevirir; Identity tipleri bu sınıfın dışına çıkmaz.
/// UserManager scoped'tır ve store'u DI'daki HalisahaDbContext'i alır: aynı scope'taki UnitOfWork'ün
/// transaction'ına katılır.
/// </summary>
internal sealed class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService
{
    // Kodlar IdentityErrorDescriber metotlarının adlarıdır; nameof derleme anında doğrular.
    private static readonly HashSet<string> PasswordErrorCodes =
    [
        nameof(IdentityErrorDescriber.PasswordTooShort),
        nameof(IdentityErrorDescriber.PasswordRequiresDigit),
        nameof(IdentityErrorDescriber.PasswordRequiresLower),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars),
    ];

    public async Task<IdentityCreationResult> CreateUserAsync(NewIdentityUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        var applicationUser = new ApplicationUser
        {
            Id = user.UserId,
            // UserName = Id (C2): hiç değişmez, e-posta değişse bile senkron tutulacak bir şey yok. Giriş e-postayla.
            UserName = user.UserId.ToString(),
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            CreatedAt = user.CreatedAt,
        };

        // Sıra (Identity 10.0.14 kaynağında doğrulandı): parola doğrulama + hash -> kullanıcı doğrulama
        // (RequireUniqueEmail: FindByEmailAsync) -> store: Context.Add + SaveChangesAsync (commit yok).
        var result = await userManager.CreateAsync(applicationUser, password);

        return result.Succeeded ? IdentityCreationResult.Success : Map(user.UserId, result.Errors);
    }

    private static IdentityCreationResult Map(Guid userId, IEnumerable<IdentityError> identityErrors)
    {
        var errors = identityErrors.ToList();

        // Önce beklenmeyenler: GUID UserName kararı altında DuplicateUserName/InvalidUserName oluşmamalı;
        // oluşursa bizim hatamızdır ve 400 gibi gösterilmez. Mesaj yalnızca kodları ve Id'yi içerir (e-posta yok:
        // log'a kişisel veri yazılmaz), teşhis için yeterli.
        var unexpected = errors.Where(e => !IsExpected(e.Code)).Select(e => e.Code).ToList();
        if (unexpected.Count > 0)
        {
            throw new InvalidOperationException(
                $"Identity returned unexpected errors while creating user {userId}: {string.Join(", ", unexpected)}.");
        }

        if (errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return IdentityCreationResult.EmailTaken;
        }

        return IdentityCreationResult.Rejected(errors
            .Select(e => new IdentityFieldError(
                PasswordErrorCodes.Contains(e.Code) ? IdentityField.Password : IdentityField.Email,
                e.Description))
            .ToList());
    }

    private static bool IsExpected(string code) =>
        PasswordErrorCodes.Contains(code)
        || code == nameof(IdentityErrorDescriber.InvalidEmail)
        || code == nameof(IdentityErrorDescriber.DuplicateEmail);
}
