using FluentValidation;
using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Commands.Login;
using Halisaha.Application.Tests.Fakes;
using Halisaha.Domain.Common;

namespace Halisaha.Application.Tests.Commands.Login;

/// <summary>
/// Handler'ın akışı sahte port'larla. Gerçek kilit ve sayaç davranışı Infrastructure testlerinde (gerçek Identity,
/// gerçek PostgreSQL); HTTP eşlemesi (401/429) Api testlerinde.
/// </summary>
public class LoginHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("3a5c7e9b-2d4f-4b6a-8c1e-5f7a9b0c2d4e");

    private readonly CallLog _log = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeIdentityService _identity;
    private readonly FakeTokenService _tokens;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_log);
        _identity = new FakeIdentityService(_log, _unitOfWork);
        _tokens = new FakeTokenService(_log);
        _handler = new LoginHandler(new LoginCommandValidator(), _identity, _tokens);
    }

    [Fact]
    public async Task ValidCredentials_VerifyOutsideAnyTransaction_ThenCreateToken()
    {
        _identity.VerificationResult = CredentialVerificationResult.Succeeded(UserId);

        await _handler.HandleAsync(new LoginCommand("ahmet@example.com", "halisaha1"), CancellationToken.None);

        // Sıra: önce doğrulama, sonra token. Transaction hiç açılmadı (uow.begin yok).
        Assert.Equal(["identity.verify (outside transaction)", "tokens.create"], _log.Calls);
    }

    [Fact]
    public async Task ValidCredentials_ReturnTheVerifiedUserAndATokenForThatUser()
    {
        _identity.VerificationResult = CredentialVerificationResult.Succeeded(UserId);

        var result = await _handler.HandleAsync(new LoginCommand("ahmet@example.com", "halisaha1"), CancellationToken.None);

        Assert.Equal(UserId, result.UserId);
        Assert.Equal(UserId, _tokens.ReceivedUserId);
        Assert.Equal($"token-for-{UserId}", result.AccessToken.Value);
        Assert.Equal(FakeTokenService.Lifetime, result.AccessToken.ExpiresIn);
    }

    [Fact]
    public async Task Email_IsTrimmedBeforeVerification_PasswordIsPassedAsIs()
    {
        await _handler.HandleAsync(new LoginCommand(" ahmet@example.com ", " halisaha1"), CancellationToken.None);

        Assert.Equal("ahmet@example.com", _identity.ReceivedEmail);
        Assert.Equal(" halisaha1", _identity.ReceivedPassword);
    }

    [Fact]
    public async Task InvalidCredentials_Throw401InvalidCredentials_AndNoTokenIsCreated()
    {
        _identity.VerificationResult = CredentialVerificationResult.InvalidCredentials;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.HandleAsync(new LoginCommand("ahmet@example.com", "yanlis-parola1"), CancellationToken.None));

        Assert.Equal(DomainErrorKind.Unauthenticated, exception.Kind);
        Assert.Equal("auth.invalid_credentials", exception.Code);
        Assert.Null(_tokens.ReceivedUserId);
    }

    [Fact]
    public async Task LockedOut_Throws429LockedOut_AndNoTokenIsCreated()
    {
        _identity.VerificationResult = CredentialVerificationResult.LockedOut;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.HandleAsync(new LoginCommand("ahmet@example.com", "halisaha1"), CancellationToken.None));

        Assert.Equal(DomainErrorKind.TooManyRequests, exception.Kind);
        Assert.Equal("auth.locked_out", exception.Code);
        Assert.Null(_tokens.ReceivedUserId);
    }

    [Fact]
    public async Task EmptyFields_ThrowValidationException_AndTouchNoPort()
    {
        // Boş parola kimlik sistemine hiç gitmez: başarısız deneme sayılmaz, hesap kilitlenmez.
        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(new LoginCommand("   ", ""), CancellationToken.None));

        Assert.Empty(_log.Calls);
    }

    [Fact]
    public async Task CancelledBeforeVerification_TouchesNoPort()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(new LoginCommand("ahmet@example.com", "halisaha1"), cancellation.Token));

        Assert.Empty(_log.Calls);
    }

    [Fact]
    public void SuccessfulResult_RequiresAUserId()
    {
        // Sahte değil, port'un kendi sözleşmesi: başarı Id'siz olamaz; Guid.Empty için token üretilmez.
        Assert.Throws<ArgumentException>(() => CredentialVerificationResult.Succeeded(Guid.Empty));
    }
}
