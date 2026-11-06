using Halisaha.Application.Abstractions.Identity;

namespace Halisaha.Application.Commands;

/// <summary>
/// Kimliği doğrulanmış kullanıcıya dönen sonuç (L7). Login bunu döner; Register, otomatik giriş diliminde aynı
/// tipe geçer. Token'ın ToString'i maskelidir (<see cref="AccessToken"/>), bu record loglansa da token görünmez.
/// </summary>
public sealed record AuthResult(Guid UserId, AccessToken AccessToken);
