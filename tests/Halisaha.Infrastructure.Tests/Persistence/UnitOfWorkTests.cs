using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Errors;
using Halisaha.Infrastructure.Identity;
using Halisaha.Infrastructure.Persistence;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Halisaha.Infrastructure.Tests.Persistence;

/// <summary>
/// UnitOfWork'ün transaction politikası (Karar 10, B3b) gerçek PostgreSQL'de. Kayıt yeterince basit olsun diye
/// yazılan veri bir il (Province). Bir şeyin kaydedilip kaydedilmediğine her zaman ayrı bir context'ten
/// (ayrı bir bağlantıdan) bakılır: aynı context kendi önbelleğini gösterir, veritabanını değil.
/// Rollback testlerinde iş kendi SaveChanges'ini çağırır; böylece veri rollback'ten önce gerçekten
/// veritabanına gitmiş olur ve "kayıt yok" sonucu bir şey kanıtlar.
/// </summary>
[Collection(PostgresCollection.Name)]
public class UnitOfWorkTests(PostgresFixture fixture)
{
    private readonly ListLogger<UnitOfWork> _logger = new();

    [Fact]
    public async Task SuccessfulWork_IsSavedAndCommitted()
    {
        var province = NewProvince();

        await using (var context = fixture.CreateContext())
        {
            // İş SaveChanges çağırmaz: kaydetmek UoW'ün işi.
            await CreateUnitOfWork(context).ExecuteInTransactionAsync(_ =>
            {
                context.Provinces.Add(province);
                return Task.CompletedTask;
            }, CancellationToken.None);
        }

        Assert.True(await ProvinceExistsAsync(province.Id));
    }

    [Fact]
    public async Task WriteInsideWork_IsInvisibleToOtherConnectionsUntilCommit()
    {
        var province = NewProvince();

        await using (var context = fixture.CreateContext())
        {
            await CreateUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
            {
                context.Provinces.Add(province);
                await context.SaveChangesAsync(ct);

                // Satır yazıldı ama commit edilmedi: Read Committed başka bağlantıya göstermez.
                Assert.False(await ProvinceExistsAsync(province.Id));
            }, CancellationToken.None);
        }

        Assert.True(await ProvinceExistsAsync(province.Id));
    }

    [Fact]
    public async Task FailingWork_IsRolledBackAndOriginalExceptionSurfaces()
    {
        var province = NewProvince();
        var original = new InvalidOperationException("work failed");

        await using (var context = fixture.CreateContext())
        {
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
                {
                    context.Provinces.Add(province);
                    await context.SaveChangesAsync(ct);
                    throw original;
                }, CancellationToken.None));

            Assert.Same(original, thrown);
        }

        Assert.False(await ProvinceExistsAsync(province.Id));
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task DomainExceptionFromWork_PassesThroughUntranslated()
    {
        var province = NewProvince();
        var original = RequestErrors.NotPending();

        await using (var context = fixture.CreateContext())
        {
            var thrown = await Assert.ThrowsAsync<DomainException>(() =>
                CreateUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
                {
                    context.Provinces.Add(province);
                    await context.SaveChangesAsync(ct);
                    throw original;
                }, CancellationToken.None));

            Assert.Same(original, thrown);
        }

        Assert.False(await ProvinceExistsAsync(province.Id));
    }

    [Fact]
    public async Task NestedCall_IsRejectedAndOuterWorkIsRolledBack()
    {
        var province = NewProvince();

        await using (var context = fixture.CreateContext())
        {
            var unitOfWork = CreateUnitOfWork(context);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    context.Provinces.Add(province);
                    await context.SaveChangesAsync(ct);
                    await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
                }, CancellationToken.None));

            AssertRejectedByNestingGuard(exception);
        }

        Assert.False(await ProvinceExistsAsync(province.Id));
    }

    [Fact]
    public async Task ManuallyOpenedTransaction_IsRejectedAndLeftUntouched()
    {
        var province = NewProvince();

        await using (var context = fixture.CreateContext())
        {
            await using var manual = await context.Database.BeginTransactionAsync();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateUnitOfWork(context).ExecuteInTransactionAsync(_ => Task.CompletedTask, CancellationToken.None));

            AssertRejectedByNestingGuard(exception);

            // UoW elle açılan transaction'ı ne kapattı ne geri aldı: hâlâ kullanılabilir ve commit edilebilir.
            Assert.Same(manual, context.Database.CurrentTransaction);
            context.Provinces.Add(province);
            await context.SaveChangesAsync();
            await manual.CommitAsync();
        }

        Assert.True(await ProvinceExistsAsync(province.Id));
    }

    [Fact]
    public async Task EmailIndexViolation_IsTranslatedLoggedAndRolledBack()
    {
        var normalizedEmail = $"{Guid.NewGuid():N}@TEST.LOCAL";
        await using (var context = fixture.CreateContext())
        {
            context.Users.Add(NewUser(normalizedEmail));
            await context.SaveChangesAsync();
        }

        var province = NewProvince();

        await using (var context = fixture.CreateContext())
        {
            var thrown = await Assert.ThrowsAsync<DomainException>(() =>
                CreateUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
                {
                    // Önce başka bir yazma kaydedilir: ihlal UoW'ün son SaveChanges'inde çıkınca bu da geri alınmalı.
                    context.Provinces.Add(province);
                    await context.SaveChangesAsync(ct);
                    context.Users.Add(NewUser(normalizedEmail));
                }, CancellationToken.None));

            Assert.Equal("auth.email_taken", thrown.Code);
            Assert.Equal(DomainErrorKind.Conflict, thrown.Kind);
        }

        Assert.False(await ProvinceExistsAsync(province.Id));

        // Çevrilmiş exception kök nedeni taşımaz; kök neden log'da.
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        var postgres = Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(entry.Exception).InnerException);
        Assert.Equal("EmailIndex", postgres.ConstraintName);
    }

    [Fact]
    public async Task ForeignKeyViolation_IsNotTranslated()
    {
        await using var context = fixture.CreateContext();

        // Var olmayan ile bağlı ilçe: 23503. Tabloda yok, 500 olarak görünür kalmalı.
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateUnitOfWork(context).ExecuteInTransactionAsync(_ =>
            {
                context.Districts.Add(new District(fixture.NextLocationId(), fixture.NextLocationId(), "Sahipsiz İlçe"));
                return Task.CompletedTask;
            }, CancellationToken.None));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task CancelledRequest_IsRolledBackWithoutTranslation()
    {
        var province = NewProvince();
        using var request = new CancellationTokenSource();

        await using (var context = fixture.CreateContext())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
                {
                    context.Provinces.Add(province);
                    await context.SaveChangesAsync(ct);
                    await request.CancelAsync(); // istemci bağlantıyı kesti
                    ct.ThrowIfCancellationRequested();
                }, request.Token));
        }

        Assert.False(await ProvinceExistsAsync(province.Id));

        // Rollback request token'ıyla yapılsaydı iptal edilmiş token yüzünden hemen hata verir ve burada
        // Error kaydı olurdu (veri yine de silinirdi: transaction'ı dispose da geri alıyor). Bu kontrol
        // "rollback request'in token'ını kullanmaz" kuralını yakalar.
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RollbackFailure_IsLoggedAndOriginalExceptionSurfaces()
    {
        var province = NewProvince();
        var original = new InvalidOperationException("work failed after the connection was lost");

        await using (var context = fixture.CreateContext())
        {
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateUnitOfWork(context).ExecuteInTransactionAsync(async ct =>
                {
                    context.Provinces.Add(province);
                    await context.SaveChangesAsync(ct);

                    // Bu transaction'ın bağlantısını başka bir bağlantıdan sonlandır: rollback artık gönderilemez.
                    var backendPid = await context.Database
                        .SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"")
                        .SingleAsync(ct);
                    await using (var admin = fixture.CreateContext())
                    {
                        await admin.Database.ExecuteSqlAsync($"SELECT pg_terminate_backend({backendPid})", ct);
                    }

                    throw original;
                }, CancellationToken.None));

            Assert.Same(original, thrown);
        }

        // Rollback başarısız oldu ama veri yine de yok: bağlantısı kopan transaction'ı PostgreSQL geri aldı.
        Assert.False(await ProvinceExistsAsync(province.Id));
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.NotSame(original, entry.Exception);
    }

    // EF Core da açık transaction'ı olan bağlantıda ikinci BeginTransaction'ı InvalidOperationException ile reddeder;
    // yalnızca tipe bakan bir test, UoW'ün kendi kontrolü kaldırılsa da yeşil kalır (sabotajla görüldü). Mesaj,
    // reddin EF'ten değil bizim kontrolümüzden geldiğini kanıtlar. Mesajın tamamı değil, kuralı söyleyen kısmı
    // aranır; ifade düzeltmesi testi kırmasın, kontrolün kaldırılması kırsın.
    private static void AssertRejectedByNestingGuard(InvalidOperationException exception) =>
        Assert.Contains("Units of work cannot be nested", exception.Message);

    private UnitOfWork CreateUnitOfWork(HalisahaDbContext context) => new(context, _logger);

    private Province NewProvince()
    {
        var id = fixture.NextLocationId();
        return new Province(id, $"UoW Test İl {id}");
    }

    private async Task<bool> ProvinceExistsAsync(int provinceId)
    {
        await using var context = fixture.CreateContext();
        return await context.Provinces.AnyAsync(p => p.Id == provinceId);
    }

    private static ApplicationUser NewUser(string normalizedEmail) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Kullanıcı",
        Email = normalizedEmail.ToLowerInvariant(),
        NormalizedEmail = normalizedEmail,
        CreatedAt = TestClock.Now,
    };
}
