using Halisaha.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Identity;

namespace Halisaha.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityService"/>'in ASP.NET Identity adapter'ı. UserManager ve SignInManager'ı kullanır ve
/// sonuçlarını (IdentityResult, SignInResult) Application'ın sonuç tiplerine çevirir; Identity tipleri bu sınıfın
/// dışına çıkmaz. UserManager scoped'tır ve store'u DI'daki HalisahaDbContext'i alır: aynı scope'taki
/// UnitOfWork'ün transaction'ına katılır.
/// </summary>
internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : IIdentityService
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

    public async Task<CredentialVerificationResult> VerifyCredentialsAsync(string email, string password)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        // NormalizedEmail üzerinden arar: büyük/küçük harf farkı burada giderilir.
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Kayıtsız e-posta: DB'ye yazılmaz, hash doğrulanmaz. Gövde parola yanlışla aynı, süre değil (L3).
            return CredentialVerificationResult.InvalidCredentials;
        }

        // CheckPasswordSignInAsync 2FA'yı hiç sormaz: 2FA'lı bir kullanıcı parola doğruysa Success alırdı ve
        // ikinci adım sessizce atlanırdı. 2FA bu akışta desteklenmiyor; açılırsa login yeniden tasarlanmalı.
        if (user.TwoFactorEnabled)
        {
            throw new InvalidOperationException(
                $"User {user.Id} has two-factor authentication enabled, which the login flow does not support.");
        }

        // Identity 10.0.12 ve 10.0.14 kaynağında aynı (Karar 11): önce kilit (parolaya bakılmaz), sonra parola.
        // Doğruysa sayaç sıfırlanır; sıfırlama eşzamanlılık yüzünden başarısızsa Failed döner. Yanlışsa sayaç
        // artırılır; 5'e ulaşınca kilit konur ve LockedOut döner. Her ikisi de kendi SaveChanges'iyle yazar.
        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return CredentialVerificationResult.Succeeded(user.Id);
        }

        if (result.IsLockedOut)
        {
            return CredentialVerificationResult.LockedOut;
        }

        // Mevcut ayarlarla oluşamaz (SignIn.RequireConfirmed* kapalı; CheckPasswordSignInAsync RequiresTwoFactor
        // döndürmez). Oluşursa bir ayar değişmiştir: 401 gibi gösterilmez, 500 olarak görünür kalır.
        if (result.IsNotAllowed || result.RequiresTwoFactor)
        {
            throw new InvalidOperationException(
                $"Unexpected sign-in result for user {user.Id}: {result}. Check the Identity SignIn options.");
        }

        return CredentialVerificationResult.InvalidCredentials;
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
