using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");

        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne(m => m.Venue)
               .WithMany()
               .HasForeignKey(m => m.VenueId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlayerProfile>()
               .WithMany()
               .HasForeignKey(m => m.OrganizerUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Participations)
               .WithOne()
               .HasForeignKey(p => p.MatchId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}