using Microsoft.Extensions.Logging;

namespace ShadowForge.Tests.Logging;

public sealed class CapturingLogger : ILogger
{
    public readonly record struct Entry(LogLevel Level, string Message, Exception? Exception);

    public List<Entry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
    }
}

public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly CapturingLogger _inner = new();

    public IReadOnlyList<CapturingLogger.Entry> Entries => _inner.Entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}
