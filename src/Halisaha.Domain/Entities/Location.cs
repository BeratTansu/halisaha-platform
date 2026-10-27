namespace Halisaha.Domain.Entities;

/// <summary>A Turkish province (il). Seeded from JSON; Id is the plate code (34 = Istanbul).</summary>
public class Province
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ICollection<District> Districts { get; private set; } = new List<District>();

    private Province() { } // EF Core uses this when reading from database

    public Province(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

/// <summary>A district (ilçe). Id = plate code * 1000 + index (34001, 34002...).</summary>
public class District
{
    public int Id { get; private set; }
    public int ProvinceId { get; private set; }
    public Province? Province { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private District() { } // EF Core

    public District(int id, int provinceId, string name)
    {
        Id = id;
        ProvinceId = provinceId;
        Name = name;
    }
}