using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Application.Errors;

namespace Halisaha.Application.Queries.GetMe;

/// <summary>
/// Giriş yapmış kullanıcının kendi bilgileri. Sorgu: transaction yok, entity yüklenmez, tek SQL komutu.
/// </summary>
public sealed class GetMeHandler(IUserQueries users)
{
    public async Task<MeResult> HandleAsync(GetMeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ActorId == Guid.Empty)
        {
            // API sub'ı kimlik doğrulamada kontrol eder; buraya boş Id gelmesi bizim hatamızdır (500).
            throw new ArgumentException("ActorId is required.", nameof(query));
        }

        // Auth sözleşmesi (Karar 11): token geçerli ama sahibi sistemde yoksa cevap 401'dir, 404 değil.
        // /me adresi her zaman var; eksik olan kimlik. İstemci oturumu kapatır.
        return await users.GetMeAsync(query.ActorId, cancellationToken)
            ?? throw AuthErrors.Unauthenticated();
    }
}
