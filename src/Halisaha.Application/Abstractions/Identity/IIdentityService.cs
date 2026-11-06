namespace Halisaha.Application.Abstractions.Identity;

/// <summary>
/// Kimlik sisteminin Application'a açılan dar kapısı. Application ASP.NET Identity tiplerini (UserManager,
/// ApplicationUser, IdentityResult) hiç görmez; yalnızca bu arayüzü ve kendi sonuç tipini bilir.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Verilen Id ile kimlik oluşturur. Beklenen sonuçlar (<see cref="IdentityCreationResult"/>) değer olarak döner;
    /// beklenmeyen her şey (DB hatası, yarışta 23505, bizim hatamızı gösteren Identity sonuçları) exception olarak
    /// yukarı çıkar. Bir <c>IUnitOfWork</c> transaction'ı içinde çağrılmalıdır: Identity kullanıcıyı hemen
    /// kaydeder (kendi SaveChanges'i), ama commit transaction'ın sahibine kalır.
    /// CancellationToken almaz: UserManager.CreateAsync token kabul etmiyor; iptal UoW'ün SaveChanges/commit
    /// adımlarında geçerli olur.
    /// </summary>
    Task<IdentityCreationResult> CreateUserAsync(NewIdentityUser user, string password);

    /// <summary>
    /// E-posta ve parolayı doğrular; kilit kontrolü ve başarısız deneme sayacı (L4) bu çağrının içindedir.
    /// Bir <c>IUnitOfWork</c> transaction'ı içinde ÇAĞRILMAZ: sayaç güncellemesi kendi SaveChanges'iyle hemen
    /// kalıcı olur; dışarıdaki bir rollback başarısız denemeyi silip kilidi atlatırdı (Karar 10).
    /// E-posta normalize edilmiş (trim) gelmelidir; büyük/küçük harf farkını kimlik sistemi kendisi giderir.
    /// CancellationToken almaz: SignInManager token kabul etmiyor.
    /// </summary>
    Task<CredentialVerificationResult> VerifyCredentialsAsync(string email, string password);
}
