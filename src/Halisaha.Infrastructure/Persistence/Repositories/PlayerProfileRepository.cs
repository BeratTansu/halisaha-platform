using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Domain.Entities;

namespace Halisaha.Infrastructure.Persistence.Repositories;

/// <summary>Kaydetmez: yalnızca context'e ekler. SaveChanges ve commit UnitOfWork'ün işidir.</summary>
internal sealed class PlayerProfileRepository(HalisahaDbContext context) : IPlayerProfileRepository
{
    public void Add(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        context.PlayerProfiles.Add(profile);
    }
}
