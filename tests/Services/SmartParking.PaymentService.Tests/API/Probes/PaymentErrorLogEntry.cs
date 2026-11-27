namespace SmartParking.PaymentService.Tests.API.Probes;

public sealed record PaymentErrorLogEntry(
    string Category,
    Microsoft.Extensions.Logging.LogLevel Level,
    string Message,
    Exception? Exception);
