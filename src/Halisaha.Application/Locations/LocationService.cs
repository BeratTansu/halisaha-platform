using Halisaha.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Application.Locations;

public sealed record ProvinceResponse(int Id, string Name);
public sealed record DistrictResponse(int Id, int ProvinceId, string Name);

public sealed class LocationService(IApplicationDbContext db)
{
    public Task<List<ProvinceResponse>> GetProvincesAsync(CancellationToken ct) =>
        db.Provinces.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new ProvinceResponse(p.Id, p.Name)).ToListAsync(ct);

    public async Task<List<DistrictResponse>> GetDistrictsAsync(int provinceId, CancellationToken ct)
    {
        if (!await db.Provinces.AnyAsync(p => p.Id == provinceId, ct))
            throw new NotFoundException("Province", provinceId);
        return await db.Districts.AsNoTracking().Where(d => d.ProvinceId == provinceId).OrderBy(d => d.Name)
            .Select(d => new DistrictResponse(d.Id, d.ProvinceId, d.Name)).ToListAsync(ct);
    }
}
