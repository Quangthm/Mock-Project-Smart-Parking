using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using SmartParking.PaymentService.API.Common;

namespace SmartParking.PaymentService.API.Middleware;

/// <summary>Logs safe request completion metadata without raw URLs, query strings, bodies, or credentials.</summary>
public sealed class PaymentRequestCompletionMiddleware(
    RequestDelegate next,
    ILogger<PaymentRequestCompletionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
        var correlation = PaymentRequestCorrelation.Get(context);
        var propagated = false;
        try
        {
            await next(context);
        }
        catch
        {
            propagated = true;
            throw;
        }
        finally
        {
            logger.LogInformation(
                "HTTP {Method} {RouteTemplate} completed in {ElapsedMilliseconds} ms with status {StatusCode}; correlation {CorrelationId}; aborted {Aborted}; propagated failure {PropagatedFailure}.",
                context.Request.Method,
                route,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                context.Response.StatusCode,
                correlation,
                context.RequestAborted.IsCancellationRequested,
                propagated);
        }
    }
}
