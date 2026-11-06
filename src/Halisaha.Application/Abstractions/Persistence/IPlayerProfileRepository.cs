using Halisaha.Domain.Entities;

namespace Halisaha.Application.Abstractions.Persistence;

/// <summary>
/// PlayerProfile aggregate'inin kaydı. Yalnızca bugünkü ihtiyaç (Register) kadar: <see cref="Add"/>.
/// Kaydetmez; değişiklikleri <see cref="IUnitOfWork"/> kaydeder ve commit eder.
/// </summary>
public interface IPlayerProfileRepository
{
    void Add(PlayerProfile profile);
}
