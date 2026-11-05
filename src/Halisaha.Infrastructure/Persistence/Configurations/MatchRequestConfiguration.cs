using Halisaha.Domain.Entities;
using Halisaha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

public class MatchRequestConfiguration : IEntityTypeConfiguration<MatchRequest>
{
    public void Configure(EntityTypeBuilder<MatchRequest> builder)
    {
        builder.ToTable("MatchRequests");

        // Id domain'de üretilir (Guid.NewGuid); EF üretilen key saymamalı.
        builder.Property(r => r.Id).ValueGeneratedNever();

        // İstek bir olayın kaydıdır (başvurdu, reddedildi); request.rejected_before bu geçmişe dayanır.
        // Bu yüzden maç veya profil silinirse geçmiş silinmez, silme reddedilir.
        builder.HasOne<Match>()
               .WithMany()
               .HasForeignKey(r => r.MatchId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlayerProfile>()
               .WithMany()
               .HasForeignKey(r => r.PlayerId)
               .OnDelete(DeleteBehavior.Restrict);

        // Aynı kolonlarda iki ayrı index var. İsimsiz HasIndex index'i kolon listesinden tanır ve ikinci
        // çağrı birinciyi ezer; isim parametreli overload her birini ayrı index yapar (ad = DB'deki ad).

        // Çiftin bütün istek geçmişi (her tür, her durum): RequestPolicy'nin pairHistory sorgusu.
        // Partial index yalnız Pending satırlarını tuttuğu için bu sorguya hizmet edemez.
        builder.HasIndex(r => new { r.MatchId, r.PlayerId }, "IX_MatchRequests_MatchId_PlayerId");

        // Bir (maç, oyuncu) çiftinde türden bağımsız en fazla bir Pending istek (Type key'de yok).
        // Kapanan istek filtreden çıkar, aynı çift yeni istek açabilir. Eşzamanlı ikinci Pending insert
        // 23505 alır; PostgresErrorTranslator bu adı request.pending_exists'e (409) çevirir.
        // Filtre enum'dan türetilir: enum değeri değişirse model değişir ve migration bunu gösterir.
        builder.HasIndex(r => new { r.MatchId, r.PlayerId }, "IX_MatchRequests_MatchId_PlayerId_Pending")
               .IsUnique()
               .HasFilter($"\"Status\" = {(int)RequestStatus.Pending}");
    }
}
