using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Infrastructure.Identity;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Infrastructure.Tests.Identity;

/// <summary>
/// IdentityService adapter'ı gerçek UserManager ve gerçek PostgreSQL ile: IdentityResult'ın Application'ın sonuç
/// tipine doğru çevrildiği, beklenmeyen sonuçların gizlenmediği ve AddInfrastructure'daki Identity ayarlarının
/// (RequireUniqueEmail, C3 parola politikası) gerçekten devrede olduğu. Her çağrı, üretimdeki gibi bir
/// UnitOfWork transaction'ı içinde yapılır.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class IdentityServiceTests(PostgresFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        _services = InfrastructureServices.Build(fixture);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    [Fact]
    public async Task CreateUser_PersistsUserWithIdentityConventions()
    {
        var user = TestUsers.New(email: $"Ahmet.{Guid.NewGuid():N}@Test.Local");

        var result = await CreateInUnitOfWorkAsync(user, TestUsers.ValidPassword);

        Assert.Equal(IdentityCreationStatus.Succeeded, result.Status);
        await using var context = fixture.CreateContext();
        var stored = await context.Users.FindAsync(user.UserId);
        Assert.NotNull(stored);
        Assert.Equal(user.UserId.ToString(), stored.UserName);          // C2: UserName = Id
        Assert.Equal(user.Email, stored.Email);                           // olduğu gibi saklanır
        Assert.Equal(user.Email.ToUpperInvariant(), stored.NormalizedEmail);
        Assert.Equal(user.FirstName, stored.FirstName);
        Assert.Equal(user.LastName, stored.LastName);
        Assert.Equal(user.PhoneNumber, stored.PhoneNumber);
        Assert.Equal(user.CreatedAt, stored.CreatedAt);
        Assert.False(string.IsNullOrEmpty(stored.PasswordHash));
        Assert.DoesNotContain(TestUsers.ValidPassword, stored.PasswordHash);
    }

    [Fact]
    public async Task SameEmail_ReturnsEmailTakenWithoutException()
    {
        var email = TestUsers.UniqueEmail();
        await CreateInUnitOfWorkAsync(TestUsers.New(email), TestUsers.ValidPassword);

        var result = await CreateInUnitOfWorkAsync(TestUsers.New(email), TestUsers.ValidPassword);

        Assert.Equal(IdentityCreationStatus.EmailTaken, result.Status);
        Assert.Equal(1, await fixture.CountUsersWithNormalizedEmailAsync(email));
    }

    [Fact]
    public async Task SameEmailWithDifferentCasing_ReturnsEmailTaken()
    {
        // Register'a park edilmiş test: PostgreSQL metin karşılaştırması büyük-küçük harfe duyarlı; tekillik
        // NormalizedEmail üzerinden sağlanır. "ahmet@x" ile "AHMET@x" aynı hesaptır.
        var email = TestUsers.UniqueEmail();
        await CreateInUnitOfWorkAsync(TestUsers.New(email), TestUsers.ValidPassword);

        var result = await CreateInUnitOfWorkAsync(TestUsers.New(email.ToUpperInvariant()), TestUsers.ValidPassword);

        Assert.Equal(IdentityCreationStatus.EmailTaken, result.Status);
        Assert.Equal(1, await fixture.CountUsersWithNormalizedEmailAsync(email));
    }

    [Theory]
    [InlineData("kisa12")]     // 8 karakterden kısa
    [InlineData("abcdefgh")]   // rakam yok
    [InlineData("12345678")]   // küçük harf yok
    public async Task PasswordViolatingPolicy_IsRejectedOnPasswordFieldAndNothingIsWritten(string password)
    {
        var user = TestUsers.New();

        var result = await CreateInUnitOfWorkAsync(user, password);

        Assert.Equal(IdentityCreationStatus.Rejected, result.Status);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, e => Assert.Equal(IdentityField.Password, e.Field));
        Assert.False(await fixture.UserExistsAsync(user.UserId));
    }

    [Theory]
    [InlineData("abcdefg1")]   // büyük harf ve sembol zorunlu değil
    [InlineData("halisaha 2026")]
    public async Task PasswordMeetingPolicy_IsAccepted(string password)
    {
        var result = await CreateInUnitOfWorkAsync(TestUsers.New(), password);

        Assert.Equal(IdentityCreationStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task EmailRejectedByIdentity_IsRejectedOnEmailField()
    {
        // FluentValidation'ın e-posta kontrolü Identity'ninkinden gevşek olabilir; bu fark 400 olarak dönmeli, 500 değil.
        var user = TestUsers.New(email: "not-an-email");

        var result = await CreateInUnitOfWorkAsync(user, TestUsers.ValidPassword);

        Assert.Equal(IdentityCreationStatus.Rejected, result.Status);
        Assert.Equal(IdentityField.Email, Assert.Single(result.Errors).Field);
        Assert.False(await fixture.UserExistsAsync(user.UserId));
    }

    [Fact]
    public async Task DuplicateUserName_IsUnexpectedAndThrowsWithDiagnosticCodes()
    {
        // GUID UserName kararı altında adapter bu durumu üretemez (UserName = kendi Id'si). Olursa bizim hatamızdır:
        // 400'e çevrilmez, 500 olarak görünür kalır. Durum, adapter'ın dışından kurulur: başka Id'li bir kullanıcı,
        // UserName olarak yeni kullanıcının Id'sini taşır.
        // (Aynı Id ile ikinci kayıt bu yolu tetiklemez: Identity sahibin Id'si aynı olduğu için DuplicateUserName
        // demez; EF iki aynı key'li nesneyi izleyemediği için InvalidOperationException fırlar. O da 500'dür.)
        var newUserId = Guid.NewGuid();
        await using (var context = fixture.CreateContext())
        {
            var squatterEmail = TestUsers.UniqueEmail();
            context.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = newUserId.ToString(),
                NormalizedUserName = newUserId.ToString().ToUpperInvariant(),
                Email = squatterEmail,
                NormalizedEmail = squatterEmail.ToUpperInvariant(),
                FirstName = "Test",
                LastName = "Kullanıcı",
                CreatedAt = TestClock.Now,
            });
            await context.SaveChangesAsync();
        }

        var email = TestUsers.UniqueEmail();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateInUnitOfWorkAsync(TestUsers.New(email, newUserId), TestUsers.ValidPassword));

        Assert.Contains("DuplicateUserName", exception.Message);
        Assert.Contains(newUserId.ToString(), exception.Message);
        Assert.DoesNotContain(email, exception.Message);   // log'a kişisel veri yazılmaz
        Assert.False(await fixture.UserExistsAsync(newUserId));
    }

    private async Task<IdentityCreationResult> CreateInUnitOfWorkAsync(NewIdentityUser user, string password)
    {
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        IdentityCreationResult? result = null;
        await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            result = await identity.CreateUserAsync(user, password);
        }, CancellationToken.None);

        return result!;
    }
}
