using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Halisaha.Api.Tests.Fixtures;

/// <summary>"500 Error seviyesinde, exception'la loglandı" gibi davranışlar ancak log'a bakılarak doğrulanır.</summary>
public sealed class ListLoggerProvider : ILoggerProvider
{
    public sealed record Entry(string Category, LogLevel Level, string Message, Exception? Exception);

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
            entries.Enqueue(new Entry(category, logLevel, formatter(state, exception), exception));
    }
}
