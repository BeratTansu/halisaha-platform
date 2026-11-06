using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Infrastructure.Persistence;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Halisaha.Infrastructure.Tests.Identity;

/// <summary>
/// Register'ın temel iddiası: UserManager.CreateAsync ile PlayerProfile insert'i aynı scoped HalisahaDbContext
/// üzerinde, aynı UnitOfWork transaction'ında çalışır; kullanıcı oluşturulduktan sonra herhangi bir adım başarısız
/// olursa iki kayıt da kalmaz. Gerçek UserManager, gerçek PostgreSQL ve üretimdeki DI kayıtları (AddInfrastructure)
/// ile; bir use case'in yaptığı gibi bütün servisler tek bir scope'tan alınır.
/// Testler handler'ı bilerek kullanmaz: konu handler'ın akışı değil transaction'ın kapsamıdır.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistrationTransactionTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly GraphBuilder _graph = new(fixture);
    private ServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        _services = InfrastructureServices.Build(fixture);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    [Fact]
    public async Task UserAndProfile_AreCommittedTogether()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var user = TestUsers.New();

        await using (var scope = _services.CreateAsyncScope())
        {
            var (unitOfWork, identity, profiles, _) = Resolve(scope);

            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                Assert.Equal(IdentityCreationStatus.Succeeded, (await identity.CreateUserAsync(user, TestUsers.ValidPassword)).Status);
                profiles.Add(NewProfile(user.UserId, districtId));
            }, CancellationToken.None);
        }

        Assert.True(await fixture.UserExistsAsync(user.UserId));
        Assert.True(await fixture.ProfileExistsAsync(user.UserId));
    }

    // Asıl kanıt. Handler normal akışta olmayan ilçeyi transaction'dan önce reddeder; burada o kontrol bilerek
    // atlanır, çünkü test edilen şey "kullanıcı INSERT edildikten sonra aynı transaction'da HERHANGİ bir hata olursa
    // kullanıcı da geri alınır" özelliğidir (üretimde bu hata bağlantı kopması, timeout ya da ileride eklenecek bir
    // kısıt olabilir). Hiç oluşturulmamış bir ilçe Id'si bu hatayı deterministik olarak, üretimdeki aynı adımda
    // (UoW'ün son SaveChanges'i, profil INSERT'i) üretir. Domain buna izin verir: ilçenin varlığı Application'ın
    // kontrolüdür, PlayerProfile constructor'ının değil.
    [Fact]
    public async Task ProfileInsertFailingAfterUserWasInserted_RollsBackBoth()
    {
        var districtThatDoesNotExist = fixture.NextLocationId(); // Id ayrıldı ama kayıt hiç oluşturulmadı
        var user = TestUsers.New();

        await using (var scope = _services.CreateAsyncScope())
        {
            var (unitOfWork, identity, profiles, context) = Resolve(scope);

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    Assert.Equal(IdentityCreationStatus.Succeeded, (await identity.CreateUserAsync(user, TestUsers.ValidPassword)).Status);

                    // A: Identity kullanıcıyı gerçekten INSERT etti. Aynı context (= aynı bağlantı ve transaction)
                    // commit edilmemiş kendi yazmasını görür. Bu olmadan sondaki "kullanıcı yok" sonucu, hiç
                    // yazılmamış olmaktan da kaynaklanabilirdi.
                    Assert.True(await context.Users.AnyAsync(u => u.Id == user.UserId, ct));

                    // B: ama commit edilmedi. Ayrı bir bağlantı görmez; Identity ayrı bir context/bağlantıda
                    // autocommit yapsaydı burada görünürdü.
                    Assert.False(await fixture.UserExistsAsync(user.UserId));

                    profiles.Add(NewProfile(user.UserId, districtThatDoesNotExist));
                }, CancellationToken.None));

            // Hata gerçekten profil INSERT'inden geldi (kullanıcınınkinden değil) ve çevrilmedi (23503 tabloda yok).
            var postgres = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
            Assert.Equal("PlayerProfiles", postgres.TableName);
        }

        Assert.False(await fixture.UserExistsAsync(user.UserId));
        Assert.False(await fixture.ProfileExistsAsync(user.UserId));
    }

    [Fact]
    public async Task FailureAfterUserWasCreated_RollsBackUser()
    {
        var user = TestUsers.New();
        var original = new InvalidOperationException("a later step of the use case failed");

        await using (var scope = _services.CreateAsyncScope())
        {
            var (unitOfWork, identity, _, _) = Resolve(scope);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync(async _ =>
                {
                    Assert.Equal(IdentityCreationStatus.Succeeded, (await identity.CreateUserAsync(user, TestUsers.ValidPassword)).Status);
                    throw original;
                }, CancellationToken.None));

            Assert.Same(original, thrown);
        }

        Assert.False(await fixture.UserExistsAsync(user.UserId));
    }

    // Yarış yolu: iki kayıt aynı e-postayla aynı anda. Kaybedenin Identity kontrolü (FindByEmailAsync) kazananın
    // commit edilmemiş satırını göremez ve geçer; INSERT'i EmailIndex'te kazananın transaction'ını bekler.
    // Kazanan commit edince kaybedenin INSERT'i 23505 alır, UoW bunu auth.email_taken'a çevirir.
    // Sıra deterministik: kazanan, kaybedenin gerçekten kilit beklediğini pg_stat_activity'de görmeden commit etmez.
    // Aksi halde kaybeden kazananın satırını commit'ten sonra görür ve test normal yoldan (EmailTaken sonucu)
    // geçerdi; o yüzden kaybedenin bir exception aldığı, bir sonuç almadığı doğrulanır.
    [Fact]
    public async Task ConcurrentRegistrationWithSameEmail_LoserGetsEmailTakenFromIndex()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var email = TestUsers.UniqueEmail();
        var winner = TestUsers.New(email);
        var loser = TestUsers.New(email);
        IdentityCreationResult? loserResult = null;
        Task? loserRegistration = null;

        await using var winnerScope = _services.CreateAsyncScope();
        await using var loserScope = _services.CreateAsyncScope();
        var (winnerUnitOfWork, winnerIdentity, winnerProfiles, _) = Resolve(winnerScope);
        var (loserUnitOfWork, loserIdentity, loserProfiles, _) = Resolve(loserScope);

        await winnerUnitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            Assert.Equal(IdentityCreationStatus.Succeeded, (await winnerIdentity.CreateUserAsync(winner, TestUsers.ValidPassword)).Status);
            winnerProfiles.Add(NewProfile(winner.UserId, districtId));

            loserRegistration = loserUnitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                loserResult = await loserIdentity.CreateUserAsync(loser, TestUsers.ValidPassword);
                loserProfiles.Add(NewProfile(loser.UserId, districtId));
            }, CancellationToken.None);

            await WaitUntilASessionIsWaitingForALockAsync();
        }, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() => loserRegistration!);
        Assert.Equal("auth.email_taken", exception.Code);
        Assert.Null(loserResult); // normal yol değil: CreateUserAsync bir sonuç döndürmeden exception aldı

        Assert.Equal(1, await fixture.CountUsersWithNormalizedEmailAsync(email));
        Assert.True(await fixture.ProfileExistsAsync(winner.UserId));
        Assert.False(await fixture.UserExistsAsync(loser.UserId));
        Assert.False(await fixture.ProfileExistsAsync(loser.UserId));
    }

    private static (IUnitOfWork, IIdentityService, IPlayerProfileRepository, HalisahaDbContext) Resolve(AsyncServiceScope scope) => (
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
        scope.ServiceProvider.GetRequiredService<IIdentityService>(),
        scope.ServiceProvider.GetRequiredService<IPlayerProfileRepository>(),
        scope.ServiceProvider.GetRequiredService<HalisahaDbContext>());

    private static PlayerProfile NewProfile(Guid userId, int districtId) =>
        new(userId, Position.Goalkeeper, skillLevel: 3, districtId);

    private async Task WaitUntilASessionIsWaitingForALockAsync()
    {
        await using var context = fixture.CreateContext();
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (true)
        {
            var waiting = await context.Database
                .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND datname = current_database()""")
                .SingleAsync();

            if (waiting > 0)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The second registration never blocked on EmailIndex; the race was not reproduced.");
            }

            await Task.Delay(20);
        }
    }
}
