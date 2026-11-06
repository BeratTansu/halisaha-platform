using Halisaha.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Persistence.Queries;

internal sealed class LocationQueries(HalisahaDbContext context) : ILocationQueries
{
    // AnyAsync entity yüklemez (SELECT EXISTS); change tracker'a bir şey girmez.
    public Task<bool> DistrictExistsAsync(int districtId, CancellationToken cancellationToken) =>
        context.Districts.AnyAsync(d => d.Id == districtId, cancellationToken);
}
