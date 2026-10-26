using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using UserService.API.Common;

namespace UserService.API.Middleware;

/// <summary>Singleton handler; all dependencies are immutable or stateless.</summary>
public sealed class ApiExceptionHandler(ApiExceptionMapper mapper, ApiErrorWriter writer,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        // The framework handles request-abort cancellation and propagates started
        // response failures before calling us. Keep this guard for direct callers.
        if (httpContext.Response.HasStarted || httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var result = mapper.Map(exception);
        if (!await writer.TryWriteAsync(httpContext, result.Response, cancellationToken))
            return false;

        if (result.IsUnexpected)
        {
            // Do not pass the Exception object: SQL and arbitrary exception messages
            // may contain secrets/PII. Framework diagnostics for handled errors are suppressed.
            logger.LogError("Request failed with {ExceptionType}; correlation {CorrelationId}.",
                exception.GetType().FullName, Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier);
        }
        return true;
    }
}
