namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>Sabit "şimdi". Application.Tests'teki eşinin aynısı; ek paket (TimeProvider.Testing) gerekmesin.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
