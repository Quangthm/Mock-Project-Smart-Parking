using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SmartParking.PaymentService.Tests.API.Probes;

internal sealed class PaymentErrorCaptureLogger(
    string category,
    ConcurrentQueue<PaymentErrorLogEntry> entries) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        entries.Enqueue(new PaymentErrorLogEntry(category, logLevel, formatter(state, exception), exception));
}
