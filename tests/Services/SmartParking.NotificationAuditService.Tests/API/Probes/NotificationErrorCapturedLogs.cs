using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

internal sealed class NotificationErrorCapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<NotificationErrorLogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new NotificationErrorCaptureLogger(categoryName, Entries);

    public void Dispose()
    {
    }
}
