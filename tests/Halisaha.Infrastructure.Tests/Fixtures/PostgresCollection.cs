namespace Halisaha.Infrastructure.Tests.Fixtures;

/// <summary>
/// Bu collection'daki bütün test sınıfları tek bir <see cref="PostgresFixture"/> paylaşır.
/// xUnit v2'de aynı collection'daki testler sırayla çalışır; paylaşılan DB'de eşzamanlı yazma olmaz.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
