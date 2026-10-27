using FluentValidation;
using Halisaha.Application.Common;
using Halisaha.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Application.Matches;

/// <summary>After the match: completion, attendance and ratings.</summary>
public sealed class MatchResultService(
    IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock, MatchService matches,
    IValidator<RecordAttendanceRequest> attendanceValidator, IValidator<RateRequest> rateValidator)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<MatchDetailResponse> CompleteAsync(Guid matchId, CancellationToken ct)
    {
        var match = await db.Matches.SingleOrDefaultAsync(m => m.Id == matchId, ct) ?? throw new NotFoundException("Match", matchId);
        match.Complete(currentUser.UserId, Now);
        await db.SaveChangesAsync(ct);
        return await matches.GetAsync(matchId, ct);
    }

    /// <summary>All items are applied or none: one wrong player id rejects the whole list.</summary>
    public async Task<MatchDetailResponse> RecordAttendanceAsync(Guid matchId, RecordAttendanceRequest request, CancellationToken ct)
    {
        await attendanceValidator.ValidateAndThrowAsync(request, ct);
        var match = await db.Matches.Include(m => m.Participations).SingleOrDefaultAsync(m => m.Id == matchId, ct)
            ?? throw new NotFoundException("Match", matchId);
        foreach (var item in request.Items)
            match.RecordAttendance(currentUser.UserId, item.PlayerId, item.Attendance, Now);
        await db.SaveChangesAsync(ct);
        return await matches.GetAsync(matchId, ct);
    }

    public async Task RateAsync(Guid matchId, RateRequest request, CancellationToken ct)
    {
        await rateValidator.ValidateAndThrowAsync(request, ct);
        var match = await db.Matches.AsNoTracking().Include(m => m.Participations).SingleOrDefaultAsync(m => m.Id == matchId, ct)
            ?? throw new NotFoundException("Match", matchId);
        var me = currentUser.UserId;
        var alreadyRated = await db.Ratings.AnyAsync(r => r.MatchId == matchId && r.RaterId == me && r.RateeId == request.RateeId, ct);

        db.Ratings.Add(RatingPolicy.Rate(match, me, request.RateeId, request.Score, alreadyRated, Now));
        await db.SaveChangesAsync(ct); // the unique index catches a parallel duplicate
    }
}
