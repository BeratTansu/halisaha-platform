namespace Halisaha.Application.Tests.Fakes;

/// <summary>Sabit "şimdi". Ek paket (Microsoft.Extensions.TimeProvider.Testing) gerekmesin diye tek satır.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
