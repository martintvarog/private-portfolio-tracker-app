using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PortfolioTrackerApp.Api.Tests;

/// <summary>Records every log line from every category so tests can assert what was — and wasn't — logged.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Lines);

    public void Dispose() { }

    private sealed class Logger(string category, ConcurrentQueue<(string, LogLevel, string)> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            // Mirror what the console logger prints: message PLUS the exception (type, message, stack).
            // An exception message can carry data the message template never would.
            lines.Enqueue((category, logLevel, exception is null
                ? formatter(state, exception)
                : formatter(state, exception) + Environment.NewLine + exception));
    }
}
