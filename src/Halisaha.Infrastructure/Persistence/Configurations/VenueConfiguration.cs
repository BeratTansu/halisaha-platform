using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("Venues");

        // Id'yi veritabanı üretir (sahaları Admin ekler); convention'ın identity kolonu bilinçli olarak korunur.

        builder.HasOne(v => v.District)
               .WithMany()
               .HasForeignKey(v => v.DistrictId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
