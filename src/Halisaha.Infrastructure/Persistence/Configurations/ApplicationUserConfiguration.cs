using Halisaha.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Halisaha.Infrastructure.Persistence.Configurations;

/// <summary>
/// Identity kullanıcı mapping'ine yalnızca bizim eklemelerimiz. Tablo adı, key, Identity'nin kendi
/// kolonları ve index'leri IdentityDbContext'ten (base.OnModelCreating) gelir; o, bu sınıftan önce çalışır.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Identity, NormalizedEmail üzerinde unique OLMAYAN "EmailIndex"i base.OnModelCreating'de açar.
        // Bu satır o index'i değiştirir, yenisini eklemez: EF index'i kolon listesinden tanır.
        // Sıra bağımlı: base'den önce çalışsaydı Identity bu ayarı geri ezerdi (bkz. HalisahaDbContext).
        // İsim parametreli HasIndex(..., "EmailIndex") kullanılmaz; o ayrı, ikinci bir index üretir.
        // RequireUniqueEmail kontrolü ile insert arasındaki yarışı bu index kapatır; 23505 + "EmailIndex"
        // PostgresErrorTranslator'da auth.email_taken'a (409) çevrilir.
        builder.HasIndex(u => u.NormalizedEmail)
               .HasDatabaseName("EmailIndex")
               .IsUnique();
    }
}
