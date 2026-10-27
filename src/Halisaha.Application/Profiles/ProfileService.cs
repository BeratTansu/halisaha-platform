using FluentValidation;
using Halisaha.Application.Common;
using Halisaha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Application.Profiles;

public sealed record UpdateProfileRequest(Position PreferredPosition, int SkillLevel, int PreferredDistrictId);

public sealed record ProfileResponse(
    Guid UserId, string FirstName, string LastName, Position PreferredPosition, int SkillLevel,
    int PreferredDistrictId, string PreferredDistrictName, string PreferredProvinceName, PlayerStats Stats);

/// <summary>Both values are computed from raw records every time; nothing is stored.</summary>
public sealed record PlayerStats(double? AverageScore, int RatingCount, double? Reliability, int ReliabilityRecordCount);

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.PreferredPosition).IsInEnum();
        RuleFor(x => x.SkillLevel).InclusiveBetween(1, 5);
        RuleFor(x => x.PreferredDistrictId).GreaterThan(0);
    }
}

public sealed class ProfileService(
    IApplicationDbContext db, IIdentityService identity, ICurrentUser currentUser,
    IValidator<UpdateProfileRequest> validator)
{
    public async Task<ProfileResponse> GetAsync(Guid userId, CancellationToken ct)
    {
        var profile = await db.PlayerProfiles.AsNoTracking()
            .Include(p => p.PreferredDistrict!).ThenInclude(d => d.Province)
            .SingleOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new NotFoundException("Player", userId);

        var names = await identity.GetNamesAsync(new[] { userId }, ct);
        var name = names.GetValueOrDefault(userId) ?? new UserName("", "");
        var stats = await GetStatsAsync(userId, ct);

        return new ProfileResponse(profile.UserId, name.FirstName, name.LastName, profile.PreferredPosition,
            profile.SkillLevel, profile.PreferredDistrictId, profile.PreferredDistrict!.Name,
            profile.PreferredDistrict.Province!.Name, stats);
    }

    public async Task<ProfileResponse> UpdateMineAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var profile = await db.PlayerProfiles.SingleOrDefaultAsync(p => p.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException("Player", currentUser.UserId);
        if (!await db.Districts.AnyAsync(d => d.Id == request.PreferredDistrictId, ct))
            throw new NotFoundException("District", request.PreferredDistrictId);

        profile.Update(request.PreferredPosition, request.SkillLevel, request.PreferredDistrictId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(profile.UserId, ct);
    }

    /// <summary>
    /// Playing score: average of ratings this player received from present players in completed matches,
    /// where this player was also present (a later attendance fix filters ratings automatically).
    /// Reliability: share of Present among attendance records in completed matches,
    /// excluding records where the player was the organizer.
    /// </summary>
    public async Task<PlayerStats> GetStatsAsync(Guid userId, CancellationToken ct)
    {
        var completed = db.Matches.AsNoTracking().Where(m => m.Status == MatchStatus.Completed);

        var reliabilityRecords = await completed
            .Where(m => m.OrganizerUserId != userId)
            .SelectMany(m => m.Participations)
            .Where(p => p.PlayerId == userId && p.Attendance != Attendance.NotRecorded)
            .Select(p => p.Attendance)
            .ToListAsync(ct);

        var scores = await db.Ratings.AsNoTracking()
            .Where(r => r.RateeId == userId)
            .Where(r => completed.Any(m => m.Id == r.MatchId
                && m.Participations.Any(p => p.PlayerId == r.RaterId && p.Attendance == Attendance.Present)
                && m.Participations.Any(p => p.PlayerId == r.RateeId && p.Attendance == Attendance.Present)))
            .Select(r => r.Score)
            .ToListAsync(ct);

        double? reliability = reliabilityRecords.Count == 0
            ? null
            : (double)reliabilityRecords.Count(a => a == Attendance.Present) / reliabilityRecords.Count;
        double? average = scores.Count == 0 ? null : Math.Round(scores.Average(), 2);

        return new PlayerStats(average, scores.Count, reliability is null ? null : Math.Round(reliability.Value, 2), reliabilityRecords.Count);
    }
}
