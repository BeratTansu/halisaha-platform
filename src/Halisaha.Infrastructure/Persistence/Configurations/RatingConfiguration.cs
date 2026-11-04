using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

public class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings");

        // Id domain'de üretilir (Guid.NewGuid); EF üretilen key saymamalı.
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.HasOne<Match>()
               .WithMany()
               .HasForeignKey(r => r.MatchId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlayerProfile>()
               .WithMany()
               .HasForeignKey(r => r.RaterId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlayerProfile>()
               .WithMany()
               .HasForeignKey(r => r.RateeId)
               .OnDelete(DeleteBehavior.Restrict);

        // Aynı (MatchId, RaterId, RateeId) üçlüsünün birden fazla kaydını DB seviyesinde engeller.
        // Eşzamanlı iki istekte (ör. çift tıklama) ikinci insert 23505 alır; PostgresErrorTranslator
        // bu index adındaki ihlali rating.already_rated'e (409) çevirir.
        builder.HasIndex(r => new { r.MatchId, r.RaterId, r.RateeId })
               .IsUnique()
               .HasDatabaseName("IX_Ratings_MatchId_RaterId_RateeId");
    }
}
