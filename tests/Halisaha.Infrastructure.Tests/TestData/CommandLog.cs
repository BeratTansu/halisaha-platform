using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Halisaha.Infrastructure.Tests.TestData;

/// <summary>
/// Veritabanına giden SQL komutlarını EF'in kendi logundan yakalar: "Microsoft.EntityFrameworkCore.Database.Command"
/// kategorisinde CommandExecuted (20101), komut başına bir kayıt. "Tek sorgu" iddiası LINQ'tan varsayılmaz, sayılır.
/// <c>InfrastructureServices.Build(fixture, s => s.AddLogging(b => b.AddProvider(log)))</c> ile eklenir.
/// </summary>
public sealed class CommandLog : ILoggerProvider
{
    private const string CommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";
    private const int CommandExecutedEventId = 20101;

    private readonly ConcurrentQueue<string> _commands = new();

    /// <summary>Çalışan komutların log mesajı (SQL metni dahil), sırayla.</summary>
    public IReadOnlyCollection<string> Commands => _commands;

    public void Clear() => _commands.Clear();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName == CommandCategory ? _commands : null);

    public void Dispose()
    {
    }

    private sealed class Logger(ConcurrentQueue<string>? commands) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => commands is not null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (commands is not null && eventId.Id == CommandExecutedEventId)
            {
                commands.Enqueue(formatter(state, exception));
            }
        }
    }
}
