using FluentValidation;
using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Commands.Register;
using Halisaha.Application.Tests.Fakes;
using Halisaha.Domain.Common;

namespace Halisaha.Application.Tests.Commands.Register;

/// <summary>
/// Handler'ın akışı sahte port'larla, veritabanı olmadan. Gerçek transaction davranışı (Identity'nin
/// SaveChanges'i dahil iki kaydın birlikte commit/rollback olması) Infrastructure testlerinde gerçek
/// PostgreSQL ile kanıtlandı; burada handler'ın o transaction'ı doğru kullandığı kanıtlanır.
/// </summary>
public class RegisterHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    private readonly CallLog _log = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeIdentityService _identity;
    private readonly FakePlayerProfileRepository _profiles;
    private readonly FakeLocationQueries _locations;
    private readonly FakeTokenService _tokens;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _unitOfWork = new FakeUnitOfWork(_log);
        _identity = new FakeIdentityService(_log, _unitOfWork);
        _profiles = new FakePlayerProfileRepository(_log, _unitOfWork);
        _locations = new FakeLocationQueries(_log, _unitOfWork);
        _tokens = new FakeTokenService(_log);
        _handler = new RegisterHandler(
            new RegisterCommandValidator(), _locations, _identity, _profiles, _unitOfWork, _tokens, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task ValidCommand_RunsStepsInOrder_IdentityAndProfileInsideOneTransaction()
    {
        await _handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None);

        Assert.Equal(
        [
            "locations.district_exists",            // transaction dışında
            "uow.begin",
            "identity.create (in transaction)",
            "profiles.add (in transaction)",        // Identity başarılı olduktan sonra, aynı transaction'da
            "uow.commit",
            "tokens.create",                        // L7: commit'ten SONRA, transaction dışında
        ], _log.Calls);
    }

    [Fact]
    public async Task ValidCommand_ReturnsAnAccessTokenForTheNewUser()
    {
        // Otomatik giriş (Dilim 4): kayıt olan kullanıcı ayrıca login olmak zorunda kalmaz.
        var result = await _handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None);

        Assert.Equal(result.UserId, _tokens.ReceivedUserId);
        Assert.Equal($"token-for-{result.UserId}", result.AccessToken.Value);
        Assert.Equal(FakeTokenService.Lifetime, result.AccessToken.ExpiresIn);
    }

    [Fact]
    public async Task EmailTaken_CreatesNoToken()
    {
        // Kayıt tamamlanmadıysa token da yok: var olmayan bir kullanıcının elinde geçerli bir token kalmaz.
        _identity.Result = IdentityCreationResult.EmailTaken;

        await Assert.ThrowsAsync<DomainException>(() => _handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None));

        Assert.Null(_tokens.ReceivedUserId);
        Assert.DoesNotContain("tokens.create", _log.Calls);
    }

    [Fact]
    public async Task CommitFailure_CreatesNoToken()
    {
        // Asıl sebep bu: token commit'ten önce üretilseydi, commit başarısız olduğunda (bağlantı koptu, yarışta
        // unique index) kullanıcı kaydedilmemiş ama token üretilmiş olurdu.
        var handler = new RegisterHandler(
            new RegisterCommandValidator(), _locations, _identity, _profiles,
            new ThrowingUnitOfWork(new InvalidOperationException("commit failed")), _tokens, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None));

        Assert.Null(_tokens.ReceivedUserId);
    }

    [Fact]
    public async Task ValidCommand_UsesOneIdForUserProfileAndResult()
    {
        var result = await _handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None);

        var user = Assert.IsType<NewIdentityUser>(_identity.ReceivedUser);
        var profile = Assert.Single(_profiles.Added);
        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.Equal(result.UserId, user.UserId);
        Assert.Equal(result.UserId, profile.UserId);
    }

    [Fact]
    public async Task ValidCommand_PassesNormalizedDataAndClockTimeToIdentity()
    {
        var command = RegisterCommands.Valid() with
        {
            Email = " ahmet@example.com ",
            FirstName = " Ahmet",
            LastName = "Yılmaz ",
            PhoneNumber = " ",
            Password = " halisaha1",
        };

        await _handler.HandleAsync(command, CancellationToken.None);

        var user = Assert.IsType<NewIdentityUser>(_identity.ReceivedUser);
        Assert.Equal("ahmet@example.com", user.Email);
        Assert.Equal("Ahmet", user.FirstName);
        Assert.Equal("Yılmaz", user.LastName);
        Assert.Null(user.PhoneNumber);
        Assert.Equal(Now, user.CreatedAt);                     // TimeProvider'dan, DB default'u değil
        Assert.Equal(" halisaha1", _identity.ReceivedPassword); // parola olduğu gibi
    }

    [Fact]
    public async Task ValidCommand_BuildsProfileFromCommand()
    {
        var command = RegisterCommands.Valid();

        await _handler.HandleAsync(command, CancellationToken.None);

        var profile = Assert.Single(_profiles.Added);
        Assert.Equal(command.PreferredPosition, profile.PreferredPosition);
        Assert.Equal(command.SkillLevel, profile.SkillLevel);
        Assert.Equal(command.PreferredDistrictId, profile.PreferredDistrictId);
        Assert.Equal(command.PreferredDistrictId, _locations.ReceivedDistrictId);
    }

    [Fact]
    public async Task InvalidShape_ThrowsValidationException_AndTouchesNoPort()
    {
        var command = RegisterCommands.Valid() with { FirstName = "   " };

        var exception = await Assert.ThrowsAsync<ValidationException>(() => _handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(nameof(RegisterCommand.FirstName), Assert.Single(exception.Errors).PropertyName);
        Assert.Empty(_log.Calls);
    }

    [Fact]
    public async Task MissingDistrict_ThrowsDistrictNotFound_BeforeAnyTransaction()
    {
        _locations.DistrictExists = false;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None));

        Assert.Equal("profile.district_not_found", exception.Code);
        Assert.Equal(DomainErrorKind.Validation, exception.Kind);
        Assert.Equal(["locations.district_exists"], _log.Calls); // transaction hiç açılmadı
    }

    [Fact]
    public async Task InvalidSkillLevel_IsRejectedByDomain_BeforePasswordIsHashed()
    {
        // Identity çağrılmadı = parola hash'lenmedi ve DB'ye hiçbir şey yazılmadı.
        var command = RegisterCommands.Valid() with { SkillLevel = 0 };

        var exception = await Assert.ThrowsAsync<DomainException>(() => _handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal("profile.invalid_skill_level", exception.Code);
        Assert.Equal(["locations.district_exists"], _log.Calls);
        Assert.Null(_identity.ReceivedUser);
    }

    [Fact]
    public async Task EmailTaken_ThrowsAuthEmailTaken_AndAddsNoProfile()
    {
        _identity.Result = IdentityCreationResult.EmailTaken;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None));

        Assert.Equal("auth.email_taken", exception.Code);
        Assert.Equal(DomainErrorKind.Conflict, exception.Kind);
        Assert.Empty(_profiles.Added);
        Assert.Equal("uow.rollback", _log.Calls[^1]); // hata transaction'ın içinden çıktı, UoW geri aldı
    }

    [Fact]
    public async Task IdentityRejection_ThrowsValidationExceptionOnCommandFields_AndAddsNoProfile()
    {
        _identity.Result = IdentityCreationResult.Rejected(
        [
            new IdentityFieldError(IdentityField.Password, "Passwords must be at least 8 characters."),
            new IdentityFieldError(IdentityField.Password, "Passwords must have at least one digit ('0'-'9')."),
            new IdentityFieldError(IdentityField.Email, "Email 'x' is invalid."),
        ]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None));

        // Validator'ın hatalarıyla aynı biçim: command'ın alan adları, Identity'nin mesajları.
        Assert.Equal(
            [nameof(RegisterCommand.Password), nameof(RegisterCommand.Password), nameof(RegisterCommand.Email)],
            exception.Errors.Select(e => e.PropertyName));
        Assert.Equal("Passwords must be at least 8 characters.", exception.Errors.First().ErrorMessage);
        Assert.Empty(_profiles.Added);
        Assert.Equal("uow.rollback", _log.Calls[^1]);
    }

    [Fact]
    public async Task FailureFromUnitOfWork_PropagatesUnchanged()
    {
        // Ör. yarışta UoW'ün çevirdiği auth.email_taken ya da çevrilmemiş bir DB hatası: handler yakalamaz.
        var original = new InvalidOperationException("database unavailable");
        var handler = new RegisterHandler(
            new RegisterCommandValidator(), _locations, _identity, _profiles, new ThrowingUnitOfWork(original),
            _tokens, new FixedTimeProvider(Now));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(RegisterCommands.Valid(), CancellationToken.None));

        Assert.Same(original, thrown);
    }

    private sealed class ThrowingUnitOfWork(Exception exception) : Abstractions.Persistence.IUnitOfWork
    {
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken) =>
            Task.FromException(exception);
    }
}
