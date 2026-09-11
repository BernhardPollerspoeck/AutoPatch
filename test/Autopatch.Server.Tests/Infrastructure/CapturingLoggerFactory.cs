using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Autopatch.Server.Tests.Infrastructure;

public sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// Logger factory that keeps every log entry in memory.
/// </summary>
public sealed class CapturingLoggerFactory : ILoggerFactory, ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
    }
}
