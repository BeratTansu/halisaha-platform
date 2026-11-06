namespace Halisaha.Application.Abstractions.Persistence;

/// <summary>İl/ilçe okuma sorguları. Yalnızca bugünkü ihtiyaç: ilçe var mı.</summary>
public interface ILocationQueries
{
    /// <summary>
    /// Register'da transaction açılmadan önce çağrılır. İlçeler silinmediği için kontrol ile insert arasında
    /// yarış riski yoktur.
    /// </summary>
    Task<bool> DistrictExistsAsync(int districtId, CancellationToken cancellationToken);
}
