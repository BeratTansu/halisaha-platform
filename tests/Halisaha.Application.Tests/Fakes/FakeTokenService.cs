using Halisaha.Application.Abstractions.Identity;

namespace Halisaha.Application.Tests.Fakes;

/// <summary>Token'ı kimin için ürettiğini kaydeder; değer, kullanıcı Id'sinden türetilir (testte okunabilsin).</summary>
public sealed class FakeTokenService(CallLog log) : ITokenService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    public Guid? ReceivedUserId { get; private set; }

    public AccessToken CreateAccessToken(Guid userId)
    {
        log.Record("tokens.create");
        ReceivedUserId = userId;
        return new AccessToken($"token-for-{userId}", Lifetime);
    }
}
