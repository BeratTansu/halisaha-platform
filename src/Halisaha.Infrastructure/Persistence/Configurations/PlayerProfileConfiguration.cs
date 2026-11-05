using Halisaha.Domain.Entities;
using Halisaha.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

public class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
{
    public void Configure(EntityTypeBuilder<PlayerProfile> builder)
    {
        builder.ToTable("PlayerProfiles");

        // PK, convention'ın aradığı "Id" / "PlayerProfileId" adlarından biri değil; açıkça belirtilir.
        builder.HasKey(p => p.UserId);

        // 1:1, ortak PK: profilin PK'sı aynı zamanda ApplicationUser'a FK.
        // HasForeignKey<PlayerProfile>: FK'yi taşıyan (bağımlı) taraf profil; yanlış tip yazılırsa FK AspNetUsers'a kurulur.
        builder.HasOne<ApplicationUser>()
               .WithOne()
               .HasForeignKey<PlayerProfile>(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.PreferredDistrict)
               .WithMany()
               .HasForeignKey(p => p.PreferredDistrictId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
