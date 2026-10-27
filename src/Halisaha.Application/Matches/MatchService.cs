using FluentValidation;
using Halisaha.Application.Common;
using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Application.Matches;

public sealed class MatchService(
    IApplicationDbContext db, IIdentityService identity, ICurrentUser currentUser, TimeProvider clock,
    IValidator<CreateMatchRequest> createValidator, IValidator<UpdateCapacityRequest> capacityValidator)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<MatchDetailResponse> CreateAsync(CreateMatchRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        await EnsureHasProfile(currentUser.UserId, ct);

        var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(v => v.Id == request.VenueId, ct)
            ?? throw new NotFoundException("Venue", request.VenueId);
        if (!venue.IsActive)
            throw DomainException.Conflict("venue.inactive", "This venue is no longer active.");

        var match = Match.Create(currentUser.UserId, request.VenueId, request.Type, request.StartsAt,
            request.Capacity, request.ExternalPlayerCount, request.Description, Now);
        db.Matches.Add(match);
        await db.SaveChangesAsync(ct);
        return await GetAsync(match.Id, ct);
    }

    public async Task<MatchDetailResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var match = await db.Matches.AsNoTracking()
            .Include(m => m.Participations)
            .Include(m => m.Venue!).ThenInclude(v => v.District)
            .SingleOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException("Match", id);

        var playerIds = match.Participations.Select(p => p.PlayerId).ToList();
        var names = await identity.GetNamesAsync(playerIds, ct);
        var positions = await db.PlayerProfiles.AsNoTracking().Where(p => playerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.PreferredPosition, ct);

        var participants = match.Participations
            .OrderByDescending(p => p.PlayerId == match.OrganizerUserId).ThenBy(p => p.JoinedAt)
            .Select(p => new ParticipantResponse(p.PlayerId, names.GetValueOrDefault(p.PlayerId)?.FullName ?? "",
                positions.GetValueOrDefault(p.PlayerId), p.PlayerId == match.OrganizerUserId, p.Team, p.Attendance))
            .ToList();

        return new MatchDetailResponse(match.Id, match.Type, match.StartsAt, match.Capacity, match.ExternalPlayerCount,
            match.MissingPlayers, match.Status, StatusLabels.For(match.Status, match.StartsAt, match.MissingPlayers, Now),
            match.Description, match.OrganizerUserId, names.GetValueOrDefault(match.OrganizerUserId)?.FullName ?? "",
            match.VenueId, match.Venue!.Name, match.Venue.Address, match.Venue.Latitude, match.Venue.Longitude,
            match.Venue.District!.Name, match.CompletedAt, participants);
    }

    /// <summary>
    /// Upcoming open matches. With a device location the results are sorted by distance (Haversine);
    /// without it they are filtered by the player's district or province and sorted by start time.
    /// </summary>
    public async Task<List<MatchSummaryResponse>> SearchAsync(MatchSearchQuery query, CancellationToken ct)
    {
        var now = Now;
        var matches = db.Matches.AsNoTracking()
            .Where(m => m.Status == MatchStatus.Open && m.StartsAt > now);
        if (query.DistrictId is not null) matches = matches.Where(m => m.Venue!.DistrictId == query.DistrictId);
        else if (query.ProvinceId is not null) matches = matches.Where(m => m.Venue!.District!.ProvinceId == query.ProvinceId);

        var rows = await matches
            .Select(m => new
            {
                m.Id, m.Type, m.StartsAt, m.Capacity, m.Status, m.VenueId,
                Missing = m.Capacity - m.ExternalPlayerCount - m.Participations.Count,
                VenueName = m.Venue!.Name, DistrictName = m.Venue.District!.Name,
                m.Venue.Latitude, m.Venue.Longitude,
            })
            .Where(r => !query.OnlyWithFreeSlots || r.Missing > 0)
            .OrderBy(r => r.StartsAt)
            .Take(200)
            .ToListAsync(ct);

        var result = rows.Select(r =>
        {
            double? distance = query.Latitude is not null && query.Longitude is not null
                ? Math.Round(Geo.DistanceKm(query.Latitude.Value, query.Longitude.Value, r.Latitude, r.Longitude), 1)
                : null;
            return new MatchSummaryResponse(r.Id, r.Type, r.StartsAt, r.Capacity, r.Missing, r.Status,
                StatusLabels.For(r.Status, r.StartsAt, r.Missing, now), r.VenueId, r.VenueName, r.DistrictName, distance);
        });

        if (query.Latitude is not null && query.Longitude is not null)
            result = result.OrderBy(r => r.DistanceKm).ThenBy(r => r.StartsAt);

        return result.Take(50).ToList();
    }

    /// <summary>Matches the current user organizes or plays in, newest first.</summary>
    public async Task<List<MatchSummaryResponse>> MineAsync(CancellationToken ct)
    {
        var me = currentUser.UserId;
        var now = Now;
        var rows = await db.Matches.AsNoTracking()
            .Where(m => m.Participations.Any(p => p.PlayerId == me))
            .OrderByDescending(m => m.StartsAt)
            .Select(m => new
            {
                m.Id, m.Type, m.StartsAt, m.Capacity, m.Status, m.VenueId,
                Missing = m.Capacity - m.ExternalPlayerCount - m.Participations.Count,
                VenueName = m.Venue!.Name, DistrictName = m.Venue.District!.Name,
            })
            .Take(100)
            .ToListAsync(ct);
        return rows.Select(r => new MatchSummaryResponse(r.Id, r.Type, r.StartsAt, r.Capacity, r.Missing, r.Status,
            StatusLabels.For(r.Status, r.StartsAt, r.Missing, now), r.VenueId, r.VenueName, r.DistrictName, null)).ToList();
    }

    public async Task<MatchDetailResponse> UpdateCapacityAsync(Guid id, UpdateCapacityRequest request, CancellationToken ct)
    {
        await capacityValidator.ValidateAndThrowAsync(request, ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var match = await LoadLockedAsync(id, ct);
        match.UpdateCapacity(currentUser.UserId, request.Capacity, request.ExternalPlayerCount, Now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        var match = await db.Matches.SingleOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException("Match", id);
        match.Cancel(currentUser.UserId, Now);
        await db.SaveChangesAsync(ct);
    }

    public async Task WithdrawAsync(Guid id, CancellationToken ct)
    {
        var match = await db.Matches.Include(m => m.Participations).SingleOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException("Match", id);
        match.Withdraw(currentUser.UserId, Now);
        await db.SaveChangesAsync(ct);
    }

    internal async Task<Match> LoadLockedAsync(Guid id, CancellationToken ct)
    {
        if (!await db.LockMatchAsync(id, ct)) throw new NotFoundException("Match", id);
        return await db.Matches.Include(m => m.Participations).SingleAsync(m => m.Id == id, ct);
    }

    private async Task EnsureHasProfile(Guid userId, CancellationToken ct)
    {
        if (!await db.PlayerProfiles.AnyAsync(p => p.UserId == userId, ct))
            throw DomainException.Forbidden("profile.required", "A player profile is required for this action.");
    }
}
