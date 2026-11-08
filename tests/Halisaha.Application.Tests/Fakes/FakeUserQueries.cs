using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Application.Queries.GetMe;

namespace Halisaha.Application.Tests.Fakes;

public sealed class FakeUserQueries : IUserQueries
{
    public MeResult? Result { get; set; }

    public Guid? ReceivedUserId { get; private set; }

    public Task<MeResult?> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        ReceivedUserId = userId;
        return Task.FromResult(Result);
    }
}
