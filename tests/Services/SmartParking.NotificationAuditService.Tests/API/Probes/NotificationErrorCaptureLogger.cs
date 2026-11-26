using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

internal sealed class NotificationErrorCaptureLogger(
    string category,
    ConcurrentQueue<NotificationErrorLogEntry> entries) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        entries.Enqueue(new NotificationErrorLogEntry(category, logLevel, formatter(state, exception), exception));
}
