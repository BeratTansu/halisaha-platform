using Halisaha.Application.Abstractions.Identity;
using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>
/// <see cref="IUserQueries.GetMeAsync"/> gerçek PostgreSQL'de: projeksiyonun alanları, profilsiz kullanıcı ve
/// "tek SQL komutu" iddiası. Komutlar EF'in kendi logundan sayılır (<see cref="CommandLog"/>).
/// Kullanıcı Register'daki gibi oluşturulur: Identity + profil, aynı UnitOfWork transaction'ında.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UserQueriesTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly GraphBuilder _graph = new(fixture);
    private readonly CommandLog _commands = new();
    private ServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        _services = InfrastructureServices.Build(fixture, services => services.AddLogging(b => b.AddProvider(_commands)));
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    [Fact]
    public async Task UserWithProfile_IsReturnedWithDistrictAndProvinceNames()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var user = await CreateUserAsync(withProfileIn: districtId);

        var me = await GetMeAsync(user.UserId);

        Assert.NotNull(me);
        Assert.Equal(user.UserId, me.UserId);
        Assert.Equal(user.Email, me.Email);
        Assert.Equal(user.FirstName, me.FirstName);
        Assert.Equal(user.LastName, me.LastName);
        Assert.Equal(user.PhoneNumber, me.PhoneNumber);
        Assert.NotNull(me.Profile);
        Assert.Equal(Position.Midfielder, me.Profile.PreferredPosition);
        Assert.Equal(4, me.Profile.SkillLevel);
        Assert.Equal(districtId, me.Profile.District.Id);
        Assert.Equal($"Test İlçe {districtId}", me.Profile.District.Name);
        // GraphBuilder ili ilçeden bir önce oluşturur (Id = ilçe Id'si - 1).
        Assert.Equal(districtId - 1, me.Profile.Province.Id);
        Assert.Equal($"Test İl {districtId - 1}", me.Profile.Province.Name);
    }

    [Fact]
    public async Task UserWithoutProfile_IsReturnedWithNullProfile()
    {
        // Admin gibi oyuncu olmayan kullanıcı (Karar 2 bileşimi): kullanıcı döner, profil null. INNER JOIN olsaydı
        // kullanıcı hiç dönmezdi ve /me 401 verirdi.
        var user = await CreateUserAsync(withProfileIn: null);

        var me = await GetMeAsync(user.UserId);

        Assert.NotNull(me);
        Assert.Equal(user.UserId, me.UserId);
        Assert.Null(me.Profile);
    }

    [Fact]
    public async Task UnknownUser_IsNull()
    {
        Assert.Null(await GetMeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Query_IsExactlyOneSqlCommand_WithLeftJoins()
    {
        var districtId = await _graph.CreateDistrictAsync();
        var user = await CreateUserAsync(withProfileIn: districtId);
        _commands.Clear(); // kurulumun komutları sayılmaz

        await GetMeAsync(user.UserId);

        var command = Assert.Single(_commands.Commands);
        Assert.Contains("FROM \"AspNetUsers\"", command);
        Assert.Contains("LEFT JOIN \"PlayerProfiles\"", command);
        Assert.Contains("\"Districts\"", command);
        Assert.Contains("\"Provinces\"", command);
        // Projeksiyon: parola hash'i ve güvenlik damgası gibi kolonlar hiç okunmaz.
        Assert.DoesNotContain("PasswordHash", command);
        Assert.DoesNotContain("SecurityStamp", command);
    }

    // ---- yardımcılar ----

    private async Task<NewIdentityUser> CreateUserAsync(int? withProfileIn)
    {
        var user = TestUsers.New();
        await using var scope = _services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var profiles = scope.ServiceProvider.GetRequiredService<IPlayerProfileRepository>();

        await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            Assert.Equal(IdentityCreationStatus.Succeeded, (await identity.CreateUserAsync(user, TestUsers.ValidPassword)).Status);
            if (withProfileIn is { } districtId)
            {
                profiles.Add(new PlayerProfile(user.UserId, Position.Midfielder, 4, districtId));
            }
        }, CancellationToken.None);

        return user;
    }

    private async Task<Application.Queries.GetMe.MeResult?> GetMeAsync(Guid userId)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserQueries>().GetMeAsync(userId, CancellationToken.None);
    }
}
