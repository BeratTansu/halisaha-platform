using Halisaha.Infrastructure.Persistence.Seed;

namespace Halisaha.Infrastructure.Tests.Seed;

/// <summary>
/// Gömülü locations.json'ın kendi tutarlılığı; veritabanı gerekmez. Sabitlenmiş Id'ler, JSON'ın yanlışlıkla yeniden
/// üretilip numaraların kaymasını yakalar (README: mevcut Id'ler asla yeniden hesaplanmaz).
/// </summary>
public class LocationDataTests
{
    private static readonly IReadOnlyList<ProvinceSeed> Provinces = LocationSeeder.LoadSeedData();

    // Kaynak veri setinde (turkiye-api 2025) büyükşehir olarak işaretli 30 il.
    private static readonly int[] MetropolitanPlates =
        [1, 6, 7, 9, 10, 16, 20, 21, 25, 26, 27, 31, 33, 34, 35, 38, 41, 42, 44, 45, 46, 47, 48, 52, 54, 55, 59, 61, 63, 65];

    [Fact]
    public void Provinces_Are81WithPlateCodeIds()
    {
        Assert.Equal(Enumerable.Range(1, 81), Provinces.Select(p => p.Id).Order());
    }

    // 973 = 922 ilçe belediyesi + büyükşehir olmayan 51 ilin merkez ilçesi.
    [Fact]
    public void Districts_Are973_519InMetropolitanAnd454InOtherProvinces_With51CentralDistricts()
    {
        var districts = Provinces.SelectMany(p => p.Districts.Select(d => (ProvinceId: p.Id, d.Name))).ToList();

        Assert.Equal(973, districts.Count);
        Assert.Equal(519, districts.Count(d => MetropolitanPlates.Contains(d.ProvinceId)));
        Assert.Equal(454, districts.Count(d => !MetropolitanPlates.Contains(d.ProvinceId)));

        var central = districts.Where(d => d.Name == "Merkez").ToList();
        Assert.Equal(51, central.Count);
        Assert.All(central, d => Assert.DoesNotContain(d.ProvinceId, MetropolitanPlates));
    }

    [Fact]
    public void DistrictIds_AreUnique_AndNamesAreUniqueWithinProvince()
    {
        var ids = Provinces.SelectMany(p => p.Districts).Select(d => d.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        Assert.All(Provinces, p => Assert.Equal(p.Districts.Count, p.Districts.Select(d => d.Name).Distinct().Count()));
        Assert.Equal(Provinces.Count, Provinces.Select(p => p.Name).Distinct().Count());
    }

    // İlk üretimdeki Türk alfabesi sırası. Yeniden sıralama bu değerlerin çoğunu kaydırır.
    [Theory]
    [InlineData(1001, 1, "Aladağ")]
    [InlineData(1015, 1, "Yüreğir")]
    [InlineData(32007, 32, "Merkez")]
    [InlineData(32010, 32, "Şarkikaraağaç")]
    [InlineData(34001, 34, "Adalar")]
    [InlineData(34039, 34, "Zeytinburnu")]
    [InlineData(55001, 55, "19 Mayıs")]
    public void PinnedDistrictIds_KeepTheirFirstGenerationValues(int id, int provinceId, string name)
    {
        var province = Assert.Single(Provinces, p => p.Districts.Any(d => d.Id == id));
        Assert.Equal(provinceId, province.Id);
        Assert.Equal(name, province.Districts.Single(d => d.Id == id).Name);
    }
}
