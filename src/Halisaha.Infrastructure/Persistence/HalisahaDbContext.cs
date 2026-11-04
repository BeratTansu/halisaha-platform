using Halisaha.Domain.Entities;
using Halisaha.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Halisaha.Infrastructure.Persistence;

public class HalisahaDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public HalisahaDbContext(DbContextOptions<HalisahaDbContext> options) : base(options)
    {
        
    }

    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchRequest> MatchRequests => Set<MatchRequest>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(HalisahaDbContext).Assembly);
    }
}