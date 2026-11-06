using Halisaha.Application.Abstractions.Identity;

namespace Halisaha.Application.Tests.Fakes;

public sealed class FakeIdentityService(CallLog log, FakeUnitOfWork unitOfWork) : IIdentityService
{
    public IdentityCreationResult Result { get; set; } = IdentityCreationResult.Success;

    public NewIdentityUser? ReceivedUser { get; private set; }

    public string? ReceivedPassword { get; private set; }

    public Task<IdentityCreationResult> CreateUserAsync(NewIdentityUser user, string password)
    {
        log.Record(unitOfWork.InTransaction ? "identity.create (in transaction)" : "identity.create (OUTSIDE transaction)");
        ReceivedUser = user;
        ReceivedPassword = password;
        return Task.FromResult(Result);
    }
}
