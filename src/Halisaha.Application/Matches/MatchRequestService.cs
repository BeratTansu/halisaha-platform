using Halisaha.Application.Common;
using Halisaha.Domain.Common;
using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Application.Matches;

/// <summary>
/// Applications and invitations. Every write locks the match row first, so the capacity check
/// and the roster change happen with no other acceptance running for the same match.
/// </summary>
public sealed class MatchRequestService(
    IApplicationDbContext db, IIdentityService identity, ICurrentUser currentUser, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    /// <summary>The current player applies to a match.</summary>
    public Task<MatchRequestResponse> ApplyAsync(Guid matchId, CancellationToken ct) =>
        SendAsync(matchId, currentUser.UserId, RequestType.Application, ct);

    /// <summary>The organizer invites a player.</summary>
    public Task<MatchRequestResponse> InviteAsync(Guid matchId, Guid playerId, CancellationToken ct) =>
        SendAsync(matchId, playerId, RequestType.Invitation, ct);

    public async Task<MatchRequestResponse> AcceptAsync(Guid requestId, CancellationToken ct)
    {
        var matchId = await MatchIdOf(requestId, ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var match = await LoadLockedAsync(matchId, ct);
        var request = await db.MatchRequests.SingleAsync(r => r.Id == requestId, ct);

        RequestPolicy.AcceptBy(currentUser.UserId, match, request, Now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ToResponse(request, ct);
    }

    public async Task<MatchRequestResponse> RejectAsync(Guid requestId, CancellationToken ct)
    {
        var request = await db.MatchRequests.SingleOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException("Request", requestId);
        var organizerId = await db.Matches.Where(m => m.Id == request.MatchId).Select(m => m.OrganizerUserId).SingleAsync(ct);
        request.Reject(currentUser.UserId, organizerId, Now);
        await db.SaveChangesAsync(ct);
        return await ToResponse(request, ct);
    }

    public async Task<MatchRequestResponse> WithdrawAsync(Guid requestId, CancellationToken ct)
    {
        var request = await db.MatchRequests.SingleOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException("Request", requestId);
        var organizerId = await db.Matches.Where(m => m.Id == request.MatchId).Select(m => m.OrganizerUserId).SingleAsync(ct);
        request.Withdraw(currentUser.UserId, organizerId, Now);
        await db.SaveChangesAsync(ct);
        return await ToResponse(request, ct);
    }

    /// <summary>Organizer: all requests of the match. Others: only their own.</summary>
    public async Task<List<MatchRequestResponse>> ListForMatchAsync(Guid matchId, CancellationToken ct)
    {
        var organizerId = await db.Matches.Where(m => m.Id == matchId).Select(m => (Guid?)m.OrganizerUserId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Match", matchId);
        var query = db.MatchRequests.AsNoTracking().Where(r => r.MatchId == matchId);
        if (organizerId != currentUser.UserId) query = query.Where(r => r.PlayerId == currentUser.UserId);
        return await ToResponses(await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct), ct);
    }

    /// <summary>Requests that wait for the current user's answer: invitations to them, applications to their matches.</summary>
    public async Task<List<MatchRequestResponse>> InboxAsync(CancellationToken ct)
    {
        var me = currentUser.UserId;
        var requests = await db.MatchRequests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Pending)
            .Where(r => (r.Type == RequestType.Invitation && r.PlayerId == me)
                     || (r.Type == RequestType.Application && db.Matches.Any(m => m.Id == r.MatchId && m.OrganizerUserId == me)))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return await ToResponses(requests, ct);
    }

    private async Task<MatchRequestResponse> SendAsync(Guid matchId, Guid playerId, RequestType type, CancellationToken ct)
    {
        if (!await db.PlayerProfiles.AnyAsync(p => p.UserId == playerId, ct))
            throw type == RequestType.Application
                ? DomainException.Forbidden("profile.required", "A player profile is required for this action.")
                : new NotFoundException("Player", playerId);

        await using var tx = await db.BeginTransactionAsync(ct);
        var match = await LoadLockedAsync(matchId, ct);
        if (type == RequestType.Invitation) match.EnsureOrganizer(currentUser.UserId);

        var existing = await db.MatchRequests.Where(r => r.MatchId == matchId && r.PlayerId == playerId).ToListAsync(ct);
        var result = RequestPolicy.Send(match, playerId, type, existing, Now);
        if (!result.AcceptedExisting) db.MatchRequests.Add(result.Request);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ToResponse(result.Request, ct);
    }

    private async Task<Guid> MatchIdOf(Guid requestId, CancellationToken ct) =>
        await db.MatchRequests.Where(r => r.Id == requestId).Select(r => (Guid?)r.MatchId).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Request", requestId);

    private async Task<Match> LoadLockedAsync(Guid matchId, CancellationToken ct)
    {
        if (!await db.LockMatchAsync(matchId, ct)) throw new NotFoundException("Match", matchId);
        return await db.Matches.Include(m => m.Participations).SingleAsync(m => m.Id == matchId, ct);
    }

    private async Task<MatchRequestResponse> ToResponse(MatchRequest r, CancellationToken ct) =>
        (await ToResponses(new[] { r }, ct))[0];

    private async Task<List<MatchRequestResponse>> ToResponses(IReadOnlyList<MatchRequest> requests, CancellationToken ct)
    {
        var names = await identity.GetNamesAsync(requests.Select(r => r.PlayerId).Distinct(), ct);
        return requests.Select(r => new MatchRequestResponse(r.Id, r.MatchId, r.PlayerId,
            names.GetValueOrDefault(r.PlayerId)?.FullName ?? "", r.Type, r.Status, r.CreatedAt, r.RespondedAt)).ToList();
    }
}
