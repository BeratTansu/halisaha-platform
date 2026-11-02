using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;

namespace Halisaha.Domain.Tests;

public class MatchRequestTests
{
    // A new request starts Pending and nothing has closed it yet.
    [Fact]
    public void New_StartsPendingWithoutClosedAt()
    {
        var match = TestData.OpenMatch();

        var request = new MatchRequest(match.Id, TestData.Mert, RequestType.Application, TestData.Now);

        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Equal(TestData.Now, request.CreatedAt);
        Assert.Null(request.ClosedAt);
        Assert.Equal(match.Id, request.MatchId);
        Assert.Equal(TestData.Mert, request.PlayerId);
    }
}
