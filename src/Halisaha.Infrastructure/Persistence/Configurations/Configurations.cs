using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Halisaha.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

// Enums are stored as text so the database stays readable and reordering an enum cannot corrupt data.

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
        b.Property(u => u.LastName).HasMaxLength(50).IsRequired();
        b.Property(u => u.PhoneNumber).HasMaxLength(20);
    }
}

internal sealed class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> b)
    {
        b.ToTable("provinces");
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(p => p.Name).IsUnique();
    }
}

internal sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> b)
    {
        b.ToTable("districts");
        b.Property(d => d.Id).ValueGeneratedNever();
        b.Property(d => d.Name).HasMaxLength(50).IsRequired();
        b.HasOne(d => d.Province).WithMany(p => p.Districts).HasForeignKey(d => d.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => new { d.ProvinceId, d.Name }).IsUnique();
    }
}

internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> b)
    {
        b.ToTable("venues");
        b.Property(v => v.Name).HasMaxLength(100).IsRequired();
        b.Property(v => v.Address).HasMaxLength(300).IsRequired();
        b.HasOne(v => v.District).WithMany().HasForeignKey(v => v.DistrictId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(v => new { v.DistrictId, v.IsActive });
    }
}

internal sealed class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
{
    public void Configure(EntityTypeBuilder<PlayerProfile> b)
    {
        b.ToTable("player_profiles");
        b.HasKey(p => p.UserId);
        // Shared primary key with the identity user: one profile per user, deleted together.
        b.HasOne<AppUser>().WithOne().HasForeignKey<PlayerProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(p => p.PreferredPosition).HasConversion<string>().HasMaxLength(20);
        b.HasOne(p => p.PreferredDistrict).WithMany().HasForeignKey(p => p.PreferredDistrictId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_player_profiles_skill_level", "skill_level BETWEEN 1 AND 5"));
    }
}

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> b)
    {
        b.ToTable("matches", t =>
        {
            t.HasCheckConstraint("ck_matches_capacity", "capacity BETWEEN 2 AND 30");
            t.HasCheckConstraint("ck_matches_external_count", "external_player_count >= 0 AND external_player_count < capacity");
        });
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.Description).HasMaxLength(1000);

        b.HasOne(m => m.Venue).WithMany().HasForeignKey(m => m.VenueId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PlayerProfile>().WithMany().HasForeignKey(m => m.OrganizerUserId).OnDelete(DeleteBehavior.Restrict);

        // The roster is part of the match: removing a participation from the collection deletes the row.
        b.HasMany(m => m.Participations).WithOne().HasForeignKey(p => p.MatchId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(m => m.Participations).HasField("_participations").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(m => new { m.Status, m.StartsAt });
        b.HasIndex(m => m.OrganizerUserId);
    }
}

internal sealed class MatchParticipationConfiguration : IEntityTypeConfiguration<MatchParticipation>
{
    public void Configure(EntityTypeBuilder<MatchParticipation> b)
    {
        b.ToTable("match_participations");
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Team).HasConversion<string>().HasMaxLength(5);
        b.Property(p => p.Attendance).HasConversion<string>().HasMaxLength(20);
        b.HasOne<PlayerProfile>().WithMany().HasForeignKey(p => p.PlayerId).OnDelete(DeleteBehavior.Restrict);
        // One participation per player per match.
        b.HasIndex(p => new { p.MatchId, p.PlayerId }).IsUnique();
        b.HasIndex(p => p.PlayerId);
    }
}

internal sealed class MatchRequestConfiguration : IEntityTypeConfiguration<MatchRequest>
{
    public void Configure(EntityTypeBuilder<MatchRequest> b)
    {
        b.ToTable("match_requests");
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne<Match>().WithMany().HasForeignKey(r => r.MatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PlayerProfile>().WithMany().HasForeignKey(r => r.PlayerId).OnDelete(DeleteBehavior.Restrict);
        // At most one pending request per player per match, whatever its type (partial unique index).
        b.HasIndex(r => new { r.MatchId, r.PlayerId })
            .IsUnique()
            .HasFilter($"status = '{nameof(RequestStatus.Pending)}'")
            .HasDatabaseName("ux_match_requests_one_pending");
        b.HasIndex(r => new { r.PlayerId, r.Status });
    }
}

internal sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> b)
    {
        b.ToTable("ratings", t =>
        {
            t.HasCheckConstraint("ck_ratings_score", "score BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_ratings_not_self", "rater_id <> ratee_id");
        });
        b.Property(r => r.Id).ValueGeneratedNever();
        b.HasOne<Match>().WithMany().HasForeignKey(r => r.MatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PlayerProfile>().WithMany().HasForeignKey(r => r.RaterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PlayerProfile>().WithMany().HasForeignKey(r => r.RateeId).OnDelete(DeleteBehavior.Restrict);
        // One rating per rater, per ratee, per match.
        b.HasIndex(r => new { r.MatchId, r.RaterId, r.RateeId }).IsUnique();
        b.HasIndex(r => r.RateeId);
    }
}
