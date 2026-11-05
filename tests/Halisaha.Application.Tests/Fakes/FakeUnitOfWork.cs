using Halisaha.Application.Abstractions.Persistence;

namespace Halisaha.Application.Tests.Fakes;

/// <summary>
/// İşi çalıştırır ve transaction'ın sınırlarını log'a yazar. Gerçek commit/rollback burada yok; o davranış
/// Infrastructure testlerinde gerçek PostgreSQL ile kanıtlandı. Bu sahte yalnızca handler'ın neyi transaction
/// İÇİNDE yaptığını görünür kılar.
/// </summary>
public sealed class FakeUnitOfWork(CallLog log) : IUnitOfWork
{
    public bool InTransaction { get; private set; }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        log.Record("uow.begin");
        InTransaction = true;
        try
        {
            await work(cancellationToken);
            log.Record("uow.commit");
        }
        catch
        {
            log.Record("uow.rollback");
            throw;
        }
        finally
        {
            InTransaction = false;
        }
    }
}
