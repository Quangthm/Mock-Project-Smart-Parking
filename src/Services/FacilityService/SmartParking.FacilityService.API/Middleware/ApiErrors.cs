using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SmartParking.FacilityService.API.Middleware;

// Service-local HTTP infrastructure; no dependency on another service's API assembly.
public static class ApiErrors
{
    private static readonly object CorrelationKey = new();
    public static string Correlation(HttpContext context)
    {
        if (context.Items[CorrelationKey] is string id) return id;
        var generated = Guid.NewGuid().ToString("N");
        context.Items[CorrelationKey] = generated;
        return generated;
    }

    public static async Task<bool> WriteAsync(HttpContext context, int status, string code,
        string message, object? errors = null, object? released = null, object? items = null,
        CancellationToken ct = default)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) return false;
        context.Response.StatusCode = status;
        context.Response.ContentLength = null;
        // Endpoints may set a stricter cache policy before writing an error; do not clobber it.
        if (context.Response.Headers.CacheControl.Count == 0)
            context.Response.Headers.CacheControl = "no-store";
        if (status == 401) context.Response.Headers.TryAdd("WWW-Authenticate", "Bearer");
        if (HttpMethods.IsHead(context.Request.Method) || status is 204 or 304) return true;
        var body = new Dictionary<string, object?>
        {
            ["success"] = false, ["code"] = code, ["message"] = message,
            ["traceId"] = Correlation(context)
        };
        if (errors is not null) body["errors"] = errors;
        if (released is not null) body["released"] = released;
        if (items is not null) body["items"] = items;
        await context.Response.WriteAsJsonAsync(body, ct);
        return true;
    }

    public static void Configure(IServiceCollection services)
    {
        services.Configure<ExceptionHandlerOptions>(options =>
        {
            options.SuppressDiagnosticsCallback = context =>
                context.ExceptionHandledBy == ExceptionHandledType.ExceptionHandlerService;
            options.ExceptionHandler = context =>
            {
                var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                if (error is not null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                return Task.CompletedTask;
            };
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = _ => new BindingErrorResult();
        });
    }

    public static void Use(IApplicationBuilder app)
    {
        // ExceptionHandlerMiddleware calls Response.Clear() and registers an OnStarting callback that forces
        // "no-cache,no-store". Register the operational-layout cache policy before that handler so this
        // OnStarting callback runs last (LIFO) and survives both the clear and the forced defaults.
        app.Use(async (context, next) =>
        {
            if (IsOperationalLayout(context.Request.Path))
            {
                context.Response.OnStarting(static state =>
                {
                    var response = (HttpResponse)state;
                    response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                    response.Headers.Vary = "Authorization";
                    return Task.CompletedTask;
                }, context.Response);
            }
            await next();
        });
        app.UseMiddleware<RequestCompletionMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async status =>
        {
            var context = status.HttpContext;
            var result = context.Response.StatusCode switch
            {
                400 => ("INVALID_REQUEST", "The request is invalid."),
                401 => ("UNAUTHENTICATED", "Authentication is required."),
                403 => ("FORBIDDEN", "Access is not permitted."),
                404 => ("NOT_FOUND", "The resource was not found."),
                405 => ("METHOD_NOT_ALLOWED", "The HTTP method is not supported."),
                413 => ("REQUEST_TOO_LARGE", "The request is too large."),
                415 => ("UNSUPPORTED_MEDIA_TYPE", "The content type is not supported."),
                429 => ("RATE_LIMITED", "Too many requests."),
                _ => ("REQUEST_FAILED", "The request could not be completed.")
            };
            await WriteAsync(context, context.Response.StatusCode, result.Item1, result.Item2,
                ct: context.RequestAborted);
        });
    }

    private static bool IsOperationalLayout(PathString path) =>
        path.StartsWithSegments("/api/parking-lots", StringComparison.OrdinalIgnoreCase) &&
        path.Value is not null &&
        path.Value.EndsWith("/operational-layout", StringComparison.OrdinalIgnoreCase);

    private sealed class BindingErrorResult : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            await WriteAsync(context.HttpContext, 400, "INVALID_REQUEST",
                "The request body or parameters are invalid.", ct: context.HttpContext.RequestAborted);
        }
    }
}

public sealed class RequestCompletionMiddleware(RequestDelegate next, ILogger<RequestCompletionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var correlation = ApiErrors.Correlation(context);
        var propagated = false;
        try { await next(context); }
        catch { propagated = true; throw; }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
            logger.LogInformation("HTTP {Method} {RouteTemplate} completed in {ElapsedMilliseconds} ms with status {StatusCode}; correlation {CorrelationId}; aborted {Aborted}; propagated failure {PropagatedFailure}.",
                context.Request.Method, route, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                context.Response.StatusCode, correlation, context.RequestAborted.IsCancellationRequested, propagated);
        }
    }
}
