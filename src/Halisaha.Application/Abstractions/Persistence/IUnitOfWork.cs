namespace Halisaha.Application.Abstractions.Persistence;

/// <summary>
/// Bir use case'in bütün yazmalarını tek bir veritabanı transaction'ında çalıştırır. Handler try/catch yazmaz:
/// commit, rollback ve kararlaştırılmış kısıt ihlallerinin <see cref="Domain.Common.DomainException"/>'a
/// çevrilmesi burada yapılır.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Transaction açar, <paramref name="work"/>'ü çalıştırır, kalan değişiklikleri kaydeder ve commit eder.
    /// Herhangi bir adım hata verirse rollback yapılır ve hata yukarı çıkar: kararlaştırılmış kısıt ihlalleri
    /// <see cref="Domain.Common.DomainException"/> olarak, geri kalan her şey olduğu gibi.
    /// İç içe çağrılamaz: açık bir transaction varken çağrılırsa <see cref="InvalidOperationException"/> fırlatır.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
