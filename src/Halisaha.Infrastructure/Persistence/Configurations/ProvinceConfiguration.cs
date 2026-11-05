using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("Provinces");

        // Id plaka kodudur ve seed JSON'ında sabittir; veritabanı üretmemeli.
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Referans veriyi koruyan bir domain kuralı yok; index seed JSON'ındaki mükerrer adı yakalar.
        builder.HasIndex(p => p.Name)
               .IsUnique()
               .HasDatabaseName("IX_Provinces_Name");

        // Province -> District ilişkisinin sahibi burası (Match -> Participations gibi parent tarafı).
        builder.HasMany(p => p.Districts)
               .WithOne(d => d.Province)
               .HasForeignKey(d => d.ProvinceId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
