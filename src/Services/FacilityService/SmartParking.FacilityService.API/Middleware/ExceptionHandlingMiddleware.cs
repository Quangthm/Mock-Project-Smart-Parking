using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Npgsql;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("The response has already started, cannot write exception response.");
            return;
        }

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        switch (exception)
        {
            case ValidationException valEx:
            {
                _logger.LogWarning(valEx, "Validation failed for request {Path} [TraceId: {TraceId}]", context.Request.Path, traceId);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                var firstError = valEx.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed.";
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = "INVALID_STRUCTURE",
                    message = firstError,
                    traceId
                });
                break;
            }

            case PostgresException pgEx when context.Request.Path.StartsWithSegments("/api/operator") &&
                (pgEx.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn):
            {
                _logger.LogError(pgEx, "Database schema unready on {Path} [TraceId: {TraceId}]", context.Request.Path, traceId);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = "PROTOTYPE_SCHEMA_NOT_READY",
                    message = "Operator data is unavailable until database reconciliation is complete.",
                    traceId
                });
                break;
            }

            case ReleasedIdentifierException relEx:
            {
                _logger.LogWarning(relEx, "Released identifier conflict on {Path} [TraceId: {TraceId}]", context.Request.Path, traceId);
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = relEx.Code,
                    message = relEx.Message,
                    released = new { id = relEx.ResourceId, code = relEx.ReleasedCode, deletedAt = relEx.DeletedAt },
                    traceId
                });
                break;
            }

            case StructureBatchException batchEx:
            {
                _logger.LogWarning(batchEx, "Structure batch operation conflict on {Path} [TraceId: {TraceId}]", context.Request.Path, traceId);
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = batchEx.Code,
                    message = batchEx.Message,
                    items = batchEx.Failures.Select(f => new { resourceId = f.ResourceId, code = f.Code, message = f.Message }),
                    traceId
                });
                break;
            }

            case StructureException structEx:
            {
                var statusCode = structEx.Code switch
                {
                    "UNAUTHENTICATED" => StatusCodes.Status401Unauthorized,
                    "FORBIDDEN" => StatusCodes.Status403Forbidden,
                    "SITE_NOT_FOUND" or "UNIT_NOT_FOUND" or "SLOT_NOT_FOUND" => StatusCodes.Status404NotFound,
                    "IDENTITY_UNAVAILABLE" or "COMMITMENTS_UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
                    "STRUCTURE_BUSY" or "STRUCTURE_IN_USE" or "STRUCTURE_CONFLICT" or "DUPLICATE_IDENTIFIER" or "NO_AVAILABLE_SLOT" => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest
                };

                if (statusCode >= 500)
                    _logger.LogError(structEx, "Structure service error on {Path} [Code: {Code}, TraceId: {TraceId}]", context.Request.Path, structEx.Code, traceId);
                else
                    _logger.LogWarning("Structure business exception on {Path} [Code: {Code}, Message: {Message}, TraceId: {TraceId}]", context.Request.Path, structEx.Code, structEx.Message, traceId);

                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = structEx.Code,
                    message = structEx.Message,
                    traceId
                });
                break;
            }

            default:
            {
                _logger.LogError(exception, "Unhandled exception occurred while processing request {Path} [TraceId: {TraceId}]", context.Request.Path, traceId);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = "INTERNAL_SERVER_ERROR",
                    message = "An unexpected error occurred. Please contact system administrator.",
                    traceId
                });
                break;
            }
        }
    }
}
