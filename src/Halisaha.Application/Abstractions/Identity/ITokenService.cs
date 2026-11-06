namespace Halisaha.Application.Abstractions.Identity;

/// <summary>
/// Kimliği doğrulanmış bir kullanıcı için access token üretir. Port Application'da, JWT implementasyonu
/// Infrastructure'da (Karar 6, Karar 11). Login parolayı doğruladıktan sonra, Register transaction commit
/// edildikten sonra çağırır; transaction içinde çağrılmaz, böylece var olmayan bir kullanıcıya token verilmez.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Token yalnızca kullanıcının Id'sini taşır (L2: <c>sub</c>); e-posta, ad ve rol token'a girmez.
    /// Senkron: imzalama bellekte yapılır, I/O yoktur.
    /// </summary>
    AccessToken CreateAccessToken(Guid userId);
}
