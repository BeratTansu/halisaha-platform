using Halisaha.Application.Queries.GetMe;
using Halisaha.Application.Tests.Fakes;
using Halisaha.Domain.Common;
using Halisaha.Domain.Enums;

namespace Halisaha.Application.Tests.Queries.GetMe;

public class GetMeHandlerTests
{
    private static readonly Guid ActorId = Guid.Parse("9e4c2a1b-6d3f-4b8a-a5c7-1f2e3d4c5b6a");

    private readonly FakeUserQueries _users = new();
    private readonly GetMeHandler _handler;

    public GetMeHandlerTests() => _handler = new GetMeHandler(_users);

    [Fact]
    public async Task ExistingUser_IsReturnedAsIs_ForTheActorFromTheToken()
    {
        var me = new MeResult(ActorId, "ahmet@example.com", "Ahmet", "Yılmaz", null,
            new PlayerProfileSummary(Position.Forward, 3, new LocationSummary(34001, "Adalar"), new LocationSummary(34, "İstanbul")));
        _users.Result = me;

        var result = await _handler.HandleAsync(new GetMeQuery(ActorId), CancellationToken.None);

        Assert.Same(me, result);
        Assert.Equal(ActorId, _users.ReceivedUserId);
    }

    [Fact]
    public async Task MissingUser_Is401Unauthenticated_Not404()
    {
        // Auth sözleşmesi (Karar 11): token geçerli ama sahibi yok -> "seni tanıyamıyorum", istemci oturumu kapatır.
        _users.Result = null;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.HandleAsync(new GetMeQuery(ActorId), CancellationToken.None));

        Assert.Equal(DomainErrorKind.Unauthenticated, exception.Kind);
        Assert.Equal("auth.unauthenticated", exception.Code);
    }

    [Fact]
    public async Task EmptyActorId_IsAProgrammingError()
    {
        // API boş sub'lı token'ı kimlik doğrulamada reddeder; buraya gelmesi bizim hatamızdır (500), 401 değil.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(new GetMeQuery(Guid.Empty), CancellationToken.None));

        Assert.Null(_users.ReceivedUserId);
    }

    [Fact]
    public void Result_ToString_DoesNotRevealPersonalData()
    {
        var me = new MeResult(ActorId, "ahmet@example.com", "Ahmet", "Yılmaz", "+90 555 123 45 67", null);

        var text = me.ToString();

        Assert.DoesNotContain("ahmet@example.com", text);
        Assert.DoesNotContain("Yılmaz", text);
        Assert.DoesNotContain("555", text);
        Assert.Contains(ActorId.ToString(), text);
    }
}
