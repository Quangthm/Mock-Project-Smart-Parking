using Microsoft.AspNetCore.Diagnostics;
using SmartParking.NotificationAuditService.API.Common;

namespace SmartParking.NotificationAuditService.API.Middleware;

/// <summary>Singleton handler; all dependencies are immutable or stateless.</summary>
public sealed class NotificationAuditExceptionHandler(
    NotificationAuditExceptionMapper mapper,
    NotificationApiErrorWriter writer,
    ILogger<NotificationAuditExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted || httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var result = mapper.Map(exception);
        if (!await writer.TryWriteAsync(httpContext, result.Response, cancellationToken))
        {
            return false;
        }

        if (result.IsUnexpected)
        {
            // Log only exception type and server-generated correlation ID to avoid leaking actor, case, or provider secrets.
            logger.LogError(
                "Notification/Audit request failed with {ExceptionType}; correlation {CorrelationId}.",
                exception.GetType().FullName,
                NotificationRequestCorrelation.Get(httpContext));
        }

        return true;
    }
}
