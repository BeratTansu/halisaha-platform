using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

public class MatchParticipationConfiguration : IEntityTypeConfiguration<MatchParticipation>
{
    public void Configure(EntityTypeBuilder<MatchParticipation> builder)
    {
        builder.ToTable("MatchParticipations");

        // Id domain'de üretilir. Bu ayar olmazsa, yüklenmiş maça AddParticipant ile eklenen katılımı
        // EF Unchanged sayar ve SaveChanges onu hiç yazmaz (hata da vermez).
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasOne<PlayerProfile>()
               .WithMany()
               .HasForeignKey(p => p.PlayerId)
               .OnDelete(DeleteBehavior.Restrict);

        // Yedek savunma: asıl koruma maç kilidi + domain'deki IsParticipant kontrolü.
        // Çeviri sözlüğünde bilerek yok; tetiklenirse kilit protokolünde bug var demektir (500).
        builder.HasIndex(p => new { p.MatchId, p.PlayerId })
               .IsUnique()
               .HasDatabaseName("IX_MatchParticipations_MatchId_PlayerId");
    }
}