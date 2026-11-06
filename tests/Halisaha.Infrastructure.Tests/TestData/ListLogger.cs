using Microsoft.Extensions.Logging;

namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>
/// Yazılan log kayıtlarını bellekte toplar. UnitOfWork'ün "rollback hatası loglandı, asıl hata fırlatıldı" ve
/// "çeviri kök nedeniyle loglandı" davranışları ancak log'a bakılarak doğrulanabilir.
/// </summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    private readonly List<Entry> _entries = [];

    public IReadOnlyList<Entry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _entries.Add(new Entry(logLevel, formatter(state, exception), exception));
}
