namespace Halisaha.Domain.Entities;

public sealed class Province
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ICollection<District> Districts { get; private set; } = new List<District>();

    private Province() { }
    public Province(int id, string name) { Id = id; Name = name; }
}

public sealed class District
{
    public int Id { get; private set; }
    public int ProvinceId { get; private set; }
    public Province? Province { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private District() { }
    public District(int id, int provinceId, string name) { Id = id; ProvinceId = provinceId; Name = name; }
}
