using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PortfolioTrackerApp.Api.Tests;

/// <summary>
/// Records every log line from every category, with its scopes, so tests can assert what was — and wasn't — logged.
/// Message = formatted message + exception (as the console logger prints) + " scopes: {...}" (as IncludeScopes prints).
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider? _scopes;

    public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Lines { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose() { }

    private sealed class Logger(string category, CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes?.Push(state);
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            if (exception is not null) text += Environment.NewLine + exception;

            var scopes = new List<string>();
            owner._scopes?.ForEachScope((scope, list) => list.Add(scope?.ToString() ?? ""), scopes);
            if (scopes.Count > 0) text += " scopes: " + string.Join(" | ", scopes);

            owner.Lines.Enqueue((category, logLevel, text));
        }
    }
}
