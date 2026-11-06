using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Infrastructure.Identity;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Halisaha.Infrastructure.Tests.Identity;

/// <summary>
/// <see cref="IIdentityService.VerifyCredentialsAsync"/> gerçek SignInManager, gerçek UserManager ve gerçek
/// PostgreSQL ile (L4). Kilit ve sayaç Identity'nin kendi kodu; burada bizim ayarlarımızla (5 deneme / 5 dk)
/// devrede olduğu ve sonuçların doğru çevrildiği kanıtlanır. Her doğrulama üretimdeki gibi kendi scope'unda
/// (ayrı DbContext) ve transaction dışında yapılır. Identity kilit zamanını gerçek saatten alır (TimeProvider
/// değil); testler saati sahtelemez, LockoutEnd değerine bakar.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CredentialVerificationTests(PostgresFixture fixture, ITestOutputHelper output) : IAsyncLifetime
{
    private const string WrongPassword = "yanlis-parola1";

    private ServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        _services = InfrastructureServices.Build(fixture);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    [Fact]
    public async Task CorrectPassword_SucceedsWithTheUsersId()
    {
        var user = await RegisterAsync();

        var result = await VerifyAsync(user.Email, TestUsers.ValidPassword);

        Assert.Equal(CredentialVerificationStatus.Succeeded, result.Status);
        Assert.Equal(user.UserId, result.UserId);
    }

    [Fact]
    public async Task Email_IsMatchedRegardlessOfCase()
    {
        var user = await RegisterAsync();

        var result = await VerifyAsync(user.Email.ToUpperInvariant(), TestUsers.ValidPassword);

        Assert.Equal(CredentialVerificationStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task UnknownEmail_IsInvalidCredentials()
    {
        var result = await VerifyAsync(TestUsers.UniqueEmail(), TestUsers.ValidPassword);

        Assert.Equal(CredentialVerificationStatus.InvalidCredentials, result.Status);
        Assert.Equal(Guid.Empty, result.UserId);
    }

    [Fact]
    public async Task WrongPassword_IsInvalidCredentials_AndIsCounted()
    {
        var user = await RegisterAsync();

        var result = await VerifyAsync(user.Email, WrongPassword);

        Assert.Equal(CredentialVerificationStatus.InvalidCredentials, result.Status);
        var stored = await LoadAsync(user.UserId);
        Assert.Equal(1, stored.AccessFailedCount); // sayaç transaction dışında, hemen kalıcı
        Assert.Null(stored.LockoutEnd);
    }

    [Fact]
    public async Task FifthWrongPassword_LocksTheAccountForFiveMinutes()
    {
        var user = await RegisterAsync();

        var results = new List<CredentialVerificationStatus>();
        var before = DateTimeOffset.UtcNow;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            results.Add((await VerifyAsync(user.Email, WrongPassword)).Status);
        }
        var after = DateTimeOffset.UtcNow;

        // İlk 4'ü 401; 5. deneme kilidi koyar ve kendisi de kilitli cevabını alır.
        Assert.Equal(
            [.. Enumerable.Repeat(CredentialVerificationStatus.InvalidCredentials, 4), CredentialVerificationStatus.LockedOut],
            results);
        var stored = await LoadAsync(user.UserId);
        Assert.NotNull(stored.LockoutEnd);
        Assert.InRange(stored.LockoutEnd.Value, before.AddMinutes(5), after.AddMinutes(5));
        Assert.Equal(0, stored.AccessFailedCount); // Identity kilit koyunca sayacı sıfırlar
    }

    [Fact]
    public async Task LockedAccount_RejectsEvenTheCorrectPassword()
    {
        var user = await RegisterAsync();
        await FailAsync(user.Email, times: 5);

        var result = await VerifyAsync(user.Email, TestUsers.ValidPassword);

        Assert.Equal(CredentialVerificationStatus.LockedOut, result.Status);
        Assert.Equal(Guid.Empty, result.UserId);
    }

    [Fact]
    public async Task CorrectPasswordBeforeTheLimit_ResetsTheCounter()
    {
        var user = await RegisterAsync();
        await FailAsync(user.Email, times: 4);

        Assert.Equal(CredentialVerificationStatus.Succeeded, (await VerifyAsync(user.Email, TestUsers.ValidPassword)).Status);
        Assert.Equal(0, (await LoadAsync(user.UserId)).AccessFailedCount);

        // Sıfırlandığı için 4 yanlış daha kilitlemez (sıfırlanmasaydı 1. yanlışta kilitlenirdi).
        await FailAsync(user.Email, times: 4);
        Assert.Equal(CredentialVerificationStatus.Succeeded, (await VerifyAsync(user.Email, TestUsers.ValidPassword)).Status);
    }

    [Fact]
    public async Task ExpiredLockout_AllowsSigningInAgain()
    {
        var user = await RegisterAsync();
        await FailAsync(user.Email, times: 5);
        await UpdateUserAsync(user.UserId, u => u.LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(-1));

        var result = await VerifyAsync(user.Email, TestUsers.ValidPassword);

        Assert.Equal(CredentialVerificationStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task RegisteredUser_HasLockoutEnabled()
    {
        // Kilit yalnızca LockoutEnabled = true olan kullanıcıda işler; değer kayıt anında AllowedForNewUsers'tan gelir.
        var user = await RegisterAsync();

        Assert.True((await LoadAsync(user.UserId)).LockoutEnabled);
    }

    [Fact]
    public async Task TwoFactorUser_IsRejectedAsUnexpected_EvenWithTheCorrectPassword()
    {
        // CheckPasswordSignInAsync 2FA'yı sormaz; koruma olmasaydı ikinci adım sessizce atlanırdı.
        var user = await RegisterAsync();
        await UpdateUserAsync(user.UserId, u => u.TwoFactorEnabled = true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifyAsync(user.Email, TestUsers.ValidPassword));

        Assert.Contains(user.UserId.ToString(), exception.Message);
        Assert.DoesNotContain(user.Email, exception.Message); // log'a kişisel veri yazılmaz
    }

    [Fact]
    public void IdentityOptions_MatchTheLoginDesign()
    {
        // Login akışı bu değerlere dayanır (L4 ve Karar 11). Biri değişirse bu test bilinçli bir karar ister.
        var options = _services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Lockout.DefaultLockoutTimeSpan);
        Assert.False(options.SignIn.RequireConfirmedEmail);
        Assert.False(options.SignIn.RequireConfirmedPhoneNumber);
        Assert.False(options.SignIn.RequireConfirmedAccount);
    }

    [Fact]
    public async Task ParallelWrongPasswords_NeverSucceedOrThrow_ButMayBeCountedLess()
    {
        // Ölçüm testi (Karar 11, kabul edilen risk): sayaç ConcurrencyStamp ile güncellenir; aynı anda gelen
        // denemelerden kaybedenler Failed döner ama sayılmaz. Burada garanti edilenler: hiçbiri başarılı olmaz,
        // hiçbiri 500'e dönüşmez, sayaç gerçek deneme sayısını aşmaz. Kaç tanesinin sayıldığı ortama bağlıdır
        // ve test çıktısına yazılır; asıl koruma IP rate limit'tir (L5).
        const int attempts = 4;
        var user = await RegisterAsync();

        var results = await Task.WhenAll(
            Enumerable.Range(0, attempts).Select(_ => Task.Run(() => VerifyAsync(user.Email, WrongPassword))));

        Assert.All(results, r => Assert.Equal(CredentialVerificationStatus.InvalidCredentials, r.Status));
        var counted = (await LoadAsync(user.UserId)).AccessFailedCount;
        Assert.InRange(counted, 1, attempts);
        output.WriteLine($"{attempts} parallel wrong attempts, counted: {counted}");
    }

    // ---- yardımcılar ----

    private async Task<NewIdentityUser> RegisterAsync()
    {
        var user = TestUsers.New();
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            var result = await identity.CreateUserAsync(user, TestUsers.ValidPassword);
            Assert.Equal(IdentityCreationStatus.Succeeded, result.Status);
        }, CancellationToken.None);

        return user;
    }

    /// <summary>Her çağrı kendi scope'unda: üretimde her login isteği kendi DbContext'ini alır.</summary>
    private async Task<CredentialVerificationResult> VerifyAsync(string email, string password)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityService>().VerifyCredentialsAsync(email, password);
    }

    private async Task FailAsync(string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await VerifyAsync(email, WrongPassword);
        }
    }

    private async Task<ApplicationUser> LoadAsync(Guid userId)
    {
        await using var context = fixture.CreateContext();
        return await context.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    private async Task UpdateUserAsync(Guid userId, Action<ApplicationUser> change)
    {
        await using var context = fixture.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Id == userId);
        change(user);
        await context.SaveChangesAsync();
    }
}
