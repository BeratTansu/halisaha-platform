using Halisaha.Application.Abstractions.Persistence;
using Halisaha.Infrastructure.Tests.Fixtures;
using Halisaha.Infrastructure.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace Halisaha.Infrastructure.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class LocationQueriesTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly GraphBuilder _graph = new(fixture);
    private ServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        _services = InfrastructureServices.Build(fixture);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _services.DisposeAsync().AsTask();

    [Fact]
    public async Task DistrictExists_ReflectsTheDatabase()
    {
        var existing = await _graph.CreateDistrictAsync();
        var missing = fixture.NextLocationId();

        await using var scope = _services.CreateAsyncScope();
        var locations = scope.ServiceProvider.GetRequiredService<ILocationQueries>();

        Assert.True(await locations.DistrictExistsAsync(existing, CancellationToken.None));
        Assert.False(await locations.DistrictExistsAsync(missing, CancellationToken.None));
    }
}
