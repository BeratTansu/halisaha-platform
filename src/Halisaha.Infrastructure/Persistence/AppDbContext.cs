using Halisaha.Application.Common;
using Halisaha.Domain.Entities;
using Halisaha.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Halisaha.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchRequest> MatchRequests => Set<MatchRequest>();
    public DbSet<Rating> Ratings => Set<Rating>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        new EfTransaction(await Database.BeginTransactionAsync(ct));

    public async Task<bool> LockMatchAsync(Guid matchId, CancellationToken ct = default)
    {
        // Row lock until the transaction ends. A second acceptance for the same match waits here.
        var ids = await Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM matches WHERE id = {matchId} FOR UPDATE")
            .ToListAsync(ct);
        return ids.Count == 1;
    }

    private sealed class EfTransaction(IDbContextTransaction inner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
