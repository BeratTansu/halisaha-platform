using System.Text.Json;
using Halisaha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Halisaha.Infrastructure.Persistence.Seed;

internal sealed record ProvinceSeed(int Id, string Name, int SourceId, IReadOnlyList<DistrictSeed> Districts);

internal sealed record DistrictSeed(int Id, string Name, int SourceId);

/// <summary>
/// İl/ilçe referans verisini gömülü locations.json'dan DB'ye senkronlar (upsert). JSON doğruluğun kaynağıdır:
/// eksik kayıt eklenir, adı veya ili farklı olan güncellenir, JSON'da olmayan DB kaydı silinmez, uyarı loglanır.
/// Eşleştirme bizim sabit Id'lerimizle yapılır; sourceId yalnızca kaynaktan JSON üretirken kullanılan metadata'dır.
/// UseSeeding (araçlar, bundle, Migrate) ve UseAsyncSeeding (MigrateAsync) aynı karşılaştırma mantığını paylaşır.
/// </summary>
public static class LocationSeeder
{
    private const string ResourceName = "Halisaha.Infrastructure.Persistence.Seed.locations.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<IReadOnlyList<ProvinceSeed>> Data = new(LoadSeedData);

    public static void Seed(DbContext context)
    {
        if (!TablesExist(context))
        {
            return;
        }

        var provinces = context.Set<Province>().ToDictionary(p => p.Id);
        var districts = context.Set<District>().ToDictionary(d => d.Id);

        if (Apply(context, provinces, districts))
        {
            context.SaveChanges();
        }
    }

    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (!await TablesExistAsync(context, cancellationToken))
        {
            return;
        }

        var provinces = await context.Set<Province>().ToDictionaryAsync(p => p.Id, cancellationToken);
        var districts = await context.Set<District>().ToDictionaryAsync(d => d.Id, cancellationToken);

        if (Apply(context, provinces, districts))
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    internal static IReadOnlyList<ProvinceSeed> LoadSeedData()
    {
        using var stream = typeof(LocationSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        return JsonSerializer.Deserialize<List<ProvinceSeed>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is empty.");
    }

    /// <summary>Bellekteki karşılaştırma; sync ve async yollar bunu paylaşır. Değişiklik varsa true döner.</summary>
    private static bool Apply(DbContext context, Dictionary<int, Province> provinces, Dictionary<int, District> districts)
    {
        var changed = false;
        var seedProvinceIds = new HashSet<int>();
        var seedDistrictIds = new HashSet<int>();

        foreach (var provinceSeed in Data.Value)
        {
            seedProvinceIds.Add(provinceSeed.Id);

            if (!provinces.TryGetValue(provinceSeed.Id, out var province))
            {
                context.Add(new Province(provinceSeed.Id, provinceSeed.Name));
                changed = true;
            }
            else if (province.Name != provinceSeed.Name)
            {
                // Domain'de güncelleme metodu yok (referans veri, iş kuralı yok): değer EF üzerinden yazılır.
                context.Entry(province).Property(p => p.Name).CurrentValue = provinceSeed.Name;
                changed = true;
            }

            foreach (var districtSeed in provinceSeed.Districts)
            {
                seedDistrictIds.Add(districtSeed.Id);

                if (!districts.TryGetValue(districtSeed.Id, out var district))
                {
                    context.Add(new District(districtSeed.Id, provinceSeed.Id, districtSeed.Name));
                    changed = true;
                    continue;
                }

                var entry = context.Entry(district);
                if (district.Name != districtSeed.Name)
                {
                    entry.Property(d => d.Name).CurrentValue = districtSeed.Name;
                    changed = true;
                }

                if (district.ProvinceId != provinceSeed.Id)
                {
                    entry.Property(d => d.ProvinceId).CurrentValue = provinceSeed.Id;
                    changed = true;
                }
            }
        }

        WarnAboutExtras(context, provinces.Keys.Except(seedProvinceIds), districts.Keys.Except(seedDistrictIds));
        return changed;
    }

    private static void WarnAboutExtras(DbContext context, IEnumerable<int> extraProvinceIds, IEnumerable<int> extraDistrictIds)
    {
        var provinces = extraProvinceIds.Order().ToList();
        var districts = extraDistrictIds.Order().ToList();
        if (provinces.Count == 0 && districts.Count == 0)
        {
            return;
        }

        // Bilinçli: fazla kayıtlar silinmez (sahalar ve profiller onlara bağlı olabilir); elle incelenir.
        var logger = context.GetService<ILoggerFactory>().CreateLogger(typeof(LocationSeeder).FullName!);
        logger.LogWarning(
            "Location seed: {ProvinceCount} province(s) and {DistrictCount} district(s) exist in the database but not in locations.json; they were not deleted. Province ids: {ProvinceIds}. District ids: {DistrictIds}.",
            provinces.Count, districts.Count, string.Join(", ", provinces), string.Join(", ", districts));
    }

    // Seed, migration geri alındıktan sonra da çağrılır (ör. database update 0); o anda tablolar olmayabilir.
    // Migration adına değil tablonun varlığına bakılır: EnsureCreated (history tablosu yok) ve
    // migration'ların yeniden adlandırılması/birleştirilmesi durumlarında da doğru çalışır.
    private static bool TablesExist(DbContext context) =>
        TableNames(context).All(name => context.Database.SqlQuery<bool>(RegclassQuery(name)).Single());

    private static async Task<bool> TablesExistAsync(DbContext context, CancellationToken cancellationToken)
    {
        foreach (var name in TableNames(context))
        {
            if (!await context.Database.SqlQuery<bool>(RegclassQuery(name)).SingleAsync(cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static FormattableString RegclassQuery(string qualifiedName) =>
        $"SELECT to_regclass({qualifiedName}) IS NOT NULL AS \"Value\"";

    private static IEnumerable<string> TableNames(DbContext context) =>
        new[] { typeof(Province), typeof(District) }.Select(type =>
        {
            var entityType = context.Model.FindEntityType(type)
                ?? throw new InvalidOperationException($"{type.Name} is not part of the model.");
            var table = $"\"{entityType.GetTableName()}\"";
            var schema = entityType.GetSchema();
            return schema is null ? table : $"\"{schema}\".{table}";
        });
}
