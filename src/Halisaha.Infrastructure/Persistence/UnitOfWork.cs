using Halisaha.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Halisaha.Infrastructure.Persistence;

/// <summary>
/// <see cref="IUnitOfWork"/>'ün EF Core uygulaması. Request scope'undaki tek <see cref="HalisahaDbContext"/>
/// üzerinde çalışır; aynı scope'tan context alan her şey (repository'ler, Identity'nin UserStore'u) bu
/// transaction'a katılır.
/// </summary>
internal sealed class UnitOfWork(HalisahaDbContext context, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    // Rollback request'in token'ını kullanmaz (istek iptal edilse de denenmeli), ama sınırsız da beklemez:
    // Npgsql RollbackAsync kendi timeout'unu ayarlamaz, bağlantının o anki okuma timeout'una kalır.
    // Süre dolarsa Npgsql bağlantıyı bozuk sayar; bağlantısı kopan transaction'ı PostgreSQL kendisi geri alır.
    internal static readonly TimeSpan RollbackTimeout = TimeSpan.FromSeconds(5);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        // İç içe çağrı (veya elle açılmış bir transaction) erken ve açık hata verir. Sessizce dıştaki transaction'a
        // katılmak, hangi commit'in gerçek olduğunu belirsizleştirirdi. Hiçbir şey başlatılmadan fırlatılır;
        // var olan transaction'a dokunulmaz.
        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already open on this DbContext. Units of work cannot be nested; " +
                "run the whole use case inside a single ExecuteInTransactionAsync call.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await work(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await TryRollbackAsync(transaction);

            // İptal çevrilmez. Handler'ın kendi DomainException'ı zincirinde PostgresException olmadığı için
            // çevirmenden olduğu gibi geçer.
            if (exception is not OperationCanceledException
                && PostgresErrorTranslator.TryTranslate(exception, out var translated))
            {
                // Çevrilmiş exception asıl hatayı taşımaz (DomainException'ın inner constructor'ı yok);
                // kök neden (SQLSTATE, constraint adı) burada loglanır.
                logger.LogInformation(exception, "Database error translated to {ErrorCode}.", translated.Code);
                throw translated;
            }

            // Asıl exception, stack trace'i bozulmadan.
            throw;
        }
    }

    // Rollback'in kendi hatası yutulur ve loglanır: asıl exception'ın yerini almamalı. Veri güvenliği
    // rollback'in başarısına bağlı değil (commit edilmemiş transaction bağlantı kapanınca geri alınır).
    private async Task TryRollbackAsync(IDbContextTransaction transaction)
    {
        using var timeout = new CancellationTokenSource(RollbackTimeout);

        try
        {
            await transaction.RollbackAsync(timeout.Token);
        }
        catch (Exception rollbackException)
        {
            logger.LogError(rollbackException, "Transaction rollback failed; the original exception is rethrown.");
        }
    }
}
