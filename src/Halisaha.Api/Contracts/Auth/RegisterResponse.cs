namespace Halisaha.Api.Contracts.Auth;

/// <summary>
/// Geçici sözleşme: Login diliminde token alanları eklenir, <c>userId</c> kalır (mevcut istemciler bozulmaz).
/// </summary>
public sealed record RegisterResponse(Guid UserId);
