namespace Halisaha.Application.Abstractions.Identity;

/// <summary>
/// Kimlik oluşturmak için gereken bilgiler; Application'ın kendi tipi, ApplicationUser değil. Parola bilerek
/// burada değil: ayrı parametre olarak geçer, böylece bu record loglansa bile parola görünmez.
/// </summary>
/// <param name="UserId">Handler'ın ürettiği Id. Aynı Id PlayerProfile'a da gider; Identity'de UserName olarak da kullanılır.</param>
/// <param name="CreatedAt">Handler'ın TimeProvider'dan aldığı UTC zaman.</param>
public sealed record NewIdentityUser(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateTimeOffset CreatedAt);
