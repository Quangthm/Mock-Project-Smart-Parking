using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace UserService.API.Middleware;

/// <summary>Logs metadata only; no actor IDs, URL/query values, credentials or bodies.</summary>
public sealed class RequestCompletionMiddleware(RequestDelegate next, ILogger<RequestCompletionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
        var correlation = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
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
            logger.LogInformation("HTTP {Method} {RouteTemplate} completed in {ElapsedMilliseconds} ms with status {StatusCode}; correlation {CorrelationId}; aborted {Aborted}; propagated failure {PropagatedFailure}.",
                context.Request.Method, route, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                context.Response.StatusCode, correlation, context.RequestAborted.IsCancellationRequested, propagated);
        }
    }
}
