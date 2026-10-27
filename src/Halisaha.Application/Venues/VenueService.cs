using FluentValidation;
using Halisaha.Application.Common;
using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Application.Venues;

public sealed record SaveVenueRequest(string Name, string Address, double Latitude, double Longitude, int DistrictId);

public sealed record VenueResponse(
    int Id, string Name, string Address, double Latitude, double Longitude,
    int DistrictId, string DistrictName, string ProvinceName, bool IsActive);

public sealed class SaveVenueRequestValidator : AbstractValidator<SaveVenueRequest>
{
    public SaveVenueRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.DistrictId).GreaterThan(0);
    }
}

/// <summary>Admins manage venues. Venues are never deleted, only deactivated.</summary>
public sealed class VenueService(IApplicationDbContext db, IValidator<SaveVenueRequest> validator)
{
    public async Task<List<VenueResponse>> ListAsync(int? districtId, bool includeInactive, CancellationToken ct)
    {
        var query = db.Venues.AsNoTracking();
        if (!includeInactive) query = query.Where(v => v.IsActive);
        if (districtId is not null) query = query.Where(v => v.DistrictId == districtId);
        return await query.OrderBy(v => v.Name).Select(Projection).ToListAsync(ct);
    }

    public async Task<VenueResponse> GetAsync(int id, CancellationToken ct) =>
        await db.Venues.AsNoTracking().Where(v => v.Id == id).Select(Projection).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Venue", id);

    public async Task<VenueResponse> CreateAsync(SaveVenueRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureDistrictExists(request.DistrictId, ct);
        var venue = new Venue(request.Name, request.Address, request.Latitude, request.Longitude, request.DistrictId);
        db.Venues.Add(venue);
        await db.SaveChangesAsync(ct);
        return await GetAsync(venue.Id, ct);
    }

    /// <summary>For corrections only. A real move should be a new venue so past matches keep the right place.</summary>
    public async Task<VenueResponse> UpdateAsync(int id, SaveVenueRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureDistrictExists(request.DistrictId, ct);
        var venue = await db.Venues.FindAsync([id], ct) ?? throw new NotFoundException("Venue", id);
        venue.SetDetails(request.Name, request.Address, request.Latitude, request.Longitude, request.DistrictId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var venue = await db.Venues.FindAsync([id], ct) ?? throw new NotFoundException("Venue", id);
        if (active) venue.Activate(); else venue.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureDistrictExists(int districtId, CancellationToken ct)
    {
        if (!await db.Districts.AnyAsync(d => d.Id == districtId, ct))
            throw new NotFoundException("District", districtId);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Venue, VenueResponse>> Projection = v =>
        new VenueResponse(v.Id, v.Name, v.Address, v.Latitude, v.Longitude, v.DistrictId,
            v.District!.Name, v.District.Province!.Name, v.IsActive);
}
