using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SmartParking.PaymentService.Tests.API.Probes;

internal sealed class PaymentErrorCapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<PaymentErrorLogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new PaymentErrorCaptureLogger(categoryName, Entries);

    public void Dispose()
    {
    }
}
