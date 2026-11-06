using Halisaha.Application.Abstractions.Identity;

namespace Halisaha.Application.Tests.Fakes;

public sealed class FakeIdentityService(CallLog log, FakeUnitOfWork unitOfWork) : IIdentityService
{
    public IdentityCreationResult Result { get; set; } = IdentityCreationResult.Success;

    public NewIdentityUser? ReceivedUser { get; private set; }

    public string? ReceivedPassword { get; private set; }

    public CredentialVerificationResult VerificationResult { get; set; } =
        CredentialVerificationResult.Succeeded(Guid.Parse("8d2f4a6c-1e3b-4c5d-9a7f-0b2c4d6e8f10"));

    public string? ReceivedEmail { get; private set; }

    public Task<IdentityCreationResult> CreateUserAsync(NewIdentityUser user, string password)
    {
        log.Record(unitOfWork.InTransaction ? "identity.create (in transaction)" : "identity.create (OUTSIDE transaction)");
        ReceivedUser = user;
        ReceivedPassword = password;
        return Task.FromResult(Result);
    }

    public Task<CredentialVerificationResult> VerifyCredentialsAsync(string email, string password)
    {
        // Login'de beklenen "OUTSIDE": sayaç güncellemesi bir transaction'ın rollback'iyle silinmemeli.
        log.Record(unitOfWork.InTransaction ? "identity.verify (in transaction)" : "identity.verify (outside transaction)");
        ReceivedEmail = email;
        ReceivedPassword = password;
        return Task.FromResult(VerificationResult);
    }
}
