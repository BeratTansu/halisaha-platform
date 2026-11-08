using Halisaha.Application.Queries.GetMe;

namespace Halisaha.Application.Abstractions.Persistence;

/// <summary>Kullanıcı okuma sorguları. Yalnızca bugünkü ihtiyaç: <c>GET /api/me</c>.</summary>
public interface IUserQueries
{
    /// <summary>
    /// Kullanıcıyı, varsa oyuncu profilini, ilçe ve il adlarıyla birlikte tek SQL komutunda getirir.
    /// Kullanıcı yoksa <c>null</c>.
    /// </summary>
    Task<MeResult?> GetMeAsync(Guid userId, CancellationToken cancellationToken);
}
