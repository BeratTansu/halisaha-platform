using Halisaha.Domain.Common;

namespace Halisaha.Application.Errors;

/// <summary>
/// Profil hataları içinde Application'ın tespit ettikleri. Seviye/pozisyon hataları domain'de (PlayerProfile);
/// ilçenin varlığı veritabanı gerektirdiği için burada.
/// </summary>
public static class ProfileErrors
{
    // 404 değil 400: 404 "URL'deki kaynak yok" demek; istek gövdesindeki geçersiz bir referans doğrulama hatasıdır.
    public static DomainException DistrictNotFound() =>
        DomainException.Validation("profile.district_not_found", "The selected district does not exist.");
}
