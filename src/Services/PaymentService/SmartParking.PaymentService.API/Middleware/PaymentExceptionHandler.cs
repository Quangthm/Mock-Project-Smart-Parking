using Microsoft.AspNetCore.Diagnostics;
using SmartParking.PaymentService.API.Common;

namespace SmartParking.PaymentService.API.Middleware;

/// <summary>Singleton handler; all dependencies are immutable or stateless.</summary>
public sealed class PaymentExceptionHandler(
    PaymentExceptionMapper mapper,
    PaymentApiErrorWriter writer,
    ILogger<PaymentExceptionHandler> logger) : IExceptionHandler
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
            // Log only exception type and server-generated correlation ID to avoid leaking SQL, PII, or provider secrets.
            logger.LogError(
                "Payment request failed with {ExceptionType}; correlation {CorrelationId}.",
                exception.GetType().FullName,
                PaymentRequestCorrelation.Get(httpContext));
        }

        return true;
    }
}
