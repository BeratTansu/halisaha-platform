namespace Halisaha.Application.Commands.Register;

/// <summary>
/// Geçici sözleşme: Login diliminde ITokenService eklenince token alanları gelir (Register ve Login ortak
/// AuthResult'a geçer). Token, transaction commit edildikten sonra üretilecek.
/// </summary>
public sealed record RegisterResult(Guid UserId);
