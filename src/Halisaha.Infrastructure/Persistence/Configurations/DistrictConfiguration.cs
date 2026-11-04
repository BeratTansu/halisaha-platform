using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("Districts");

        // Id = plaka kodu * 1000 + sıra, seed JSON'ında sabittir; veritabanı üretmemeli.
        builder.Property(d => d.Id).ValueGeneratedNever();

        // Aynı ilçe adı farklı illerde olabilir, tekillik il bazındadır.
        // ProvinceId önde olduğu için bu index ProvinceId FK'sine de hizmet eder.
        builder.HasIndex(d => new { d.ProvinceId, d.Name })
               .IsUnique()
               .HasDatabaseName("IX_Districts_ProvinceId_Name");

        // Province -> District ilişkisi ProvinceConfiguration'da tanımlı (ilişki sahipliği kuralı).
    }
}
