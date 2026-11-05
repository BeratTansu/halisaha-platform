namespace Halisaha.Infrastructure.Tests.Fixtures;

/// <summary>Seed testleri ana collection'dan ayrı, kendi container'ıyla çalışır.</summary>
[CollectionDefinition(Name)]
public sealed class LocationSeedCollection : ICollectionFixture<LocationSeedFixture>
{
    public const string Name = "PostgresLocationSeed";
}
