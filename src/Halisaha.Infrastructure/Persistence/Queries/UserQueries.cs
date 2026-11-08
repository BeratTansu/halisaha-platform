using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Application.Queries.GetMe;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Persistence.Queries;

/// <summary>
/// Okuma sorguları entity yüklemez, doğrudan DTO'ya projeksiyon yapar (Karar 6): change tracker'a bir şey girmez,
/// yalnızca gereken kolonlar okunur. Tek SQL komutu (UserQueriesTests ve MeEndpointTests komut sayısını ölçer).
/// </summary>
internal sealed class UserQueries(HalisahaDbContext context) : IUserQueries
{
    public Task<MeResult?> GetMeAsync(Guid userId, CancellationToken cancellationToken) =>
        (from user in context.Users
         where user.Id == userId
         // Profil isteğe bağlı (Admin'de yok): LEFT JOIN. ApplicationUser'da profile navigation yok
         // (Domain, Identity tipini bilmez); join açıkça yazılır.
         join profile in context.PlayerProfiles on user.Id equals profile.UserId into profiles
         from profile in profiles.DefaultIfEmpty()
         select new MeResult(
             user.Id,
             // Register her zaman yazar; Identity'nin tipi nullable.
             user.Email!,
             user.FirstName,
             user.LastName,
             user.PhoneNumber,
             profile == null
                 ? null
                 : new PlayerProfileSummary(
                     profile.PreferredPosition,
                     profile.SkillLevel,
                     new LocationSummary(profile.PreferredDistrict!.Id, profile.PreferredDistrict.Name),
                     new LocationSummary(profile.PreferredDistrict.Province!.Id, profile.PreferredDistrict.Province.Name))))
        .SingleOrDefaultAsync(cancellationToken);
}
