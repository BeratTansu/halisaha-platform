using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Halisaha.Api.Tests.Fixtures;

/// <summary>
/// "500 Error seviyesinde, exception'la loglandı" ya da "parola log'a düşmedi" gibi davranışlar ancak log'a
/// bakılarak doğrulanır. <see cref="Entry.State"/>, mesaj şablonuna girmeyen yapılandırılmış alanları da taşır:
/// bir log sağlayıcısı (ör. JSON konsolu) mesajı değil bu alanları yazabilir.
/// </summary>
public sealed class ListLoggerProvider : ILoggerProvider
{
    public sealed record Entry(string Category, LogLevel Level, string Message, Exception? Exception, string State);

    private readonly ConcurrentQueue<Entry> _entries = new();

    public IReadOnlyCollection<Entry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new Entry(category, logLevel, formatter(state, exception), exception, Describe(state)));

        private static string Describe<TState>(TState state) => state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join("; ", pairs.Select(p => $"{p.Key}={p.Value}"))
            : state?.ToString() ?? string.Empty;
    }
}
