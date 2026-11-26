using Microsoft.Extensions.Logging;

namespace SmartParking.NotificationAuditService.Tests.API.Probes;

public sealed record NotificationErrorLogEntry(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception);
