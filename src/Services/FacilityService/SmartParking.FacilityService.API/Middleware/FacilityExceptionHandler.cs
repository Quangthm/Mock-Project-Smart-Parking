using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.API.Middleware;

public sealed class FacilityExceptionHandler(ILogger<FacilityExceptionHandler> logger) : IExceptionHandler
{
    private sealed record Failure(int Status, string Code, string Message, object? Errors = null,
        object? Released = null, object? Items = null);

    // Exact types first, then explicit base-type lookup. No scoped dependencies are captured.
    private static readonly IReadOnlyDictionary<Type, Func<Exception, Failure>> Mappings =
        new Dictionary<Type, Func<Exception, Failure>>
        {
            [typeof(ValidationException)] = error => new(400, "INVALID_STRUCTURE", "Request validation failed.",
                ((ValidationException)error).Errors.Select(e => new { property = e.PropertyName, message = e.ErrorMessage }).ToArray()),
            [typeof(ReleasedIdentifierException)] = error =>
            {
                var released = (ReleasedIdentifierException)error;
                return new(409, released.Code, released.Message, Released:
                    new { id = released.ResourceId, code = released.ReleasedCode, deletedAt = released.DeletedAt });
            },
            [typeof(StructureBatchException)] = error =>
            {
                var batch = (StructureBatchException)error;
                return new(409, batch.Code, batch.Message, Items:
                    batch.Failures.Select(f => new { resourceId = f.ResourceId, code = f.Code, message = f.Message }).ToArray());
            },
            [typeof(StructureException)] = error =>
            {
                var structure = (StructureException)error;
                var status = structure.Code switch
                {
                    "UNAUTHENTICATED" => 401,
                    "FORBIDDEN" => 403,
                    "SITE_NOT_FOUND" or "UNIT_NOT_FOUND" or "SLOT_NOT_FOUND" => 404,
                    "IDENTITY_UNAVAILABLE" or "COMMITMENTS_UNAVAILABLE" => 503,
                    "STRUCTURE_BUSY" or "STRUCTURE_IN_USE" or "STRUCTURE_CONFLICT" or
                        "DUPLICATE_IDENTIFIER" or "NO_AVAILABLE_SLOT" => 409,
                    _ => 400
                };
                return new(status, structure.Code, structure.Message);
            },
            [typeof(DbUpdateConcurrencyException)] = _ => new(409, "RESOURCE_VERSION_CONFLICT", "The resource changed; reload its current state."),
            [typeof(BadHttpRequestException)] = error => new(((BadHttpRequestException)error).StatusCode,
                "INVALID_REQUEST", "The request body or parameters are invalid.")
        };

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) return false;
        Failure? failure = null;
        for (var type = exception.GetType(); type is not null; type = type.BaseType)
            if (Mappings.TryGetValue(type, out var map)) { failure = map(exception); break; }
        if (failure is null && exception is PostgresException
            { SqlState: PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn } &&
            (context.Request.Path.StartsWithSegments("/api/operator") ||
             context.Request.Path.StartsWithSegments("/internal/reservation-dependencies")))
            failure = new(503, "PROTOTYPE_SCHEMA_NOT_READY", "Facility data is unavailable until database reconciliation is complete.");
        var unexpected = failure is null;
        if (failure is null && (exception as PostgresException ??
            (exception as DbUpdateException)?.InnerException as PostgresException) is
            { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation })
        {
            failure = new(409, "STRUCTURE_CONFLICT", "A conflicting write has already claimed this resource.");
            unexpected = false;
        }
        failure ??= new(500, "INTERNAL_SERVER_ERROR", "An unexpected error occurred.");
        if (!await ApiErrors.WriteAsync(context, failure.Status, failure.Code, failure.Message,
            failure.Errors, failure.Released, failure.Items, ct)) return false;
        if (unexpected)
            logger.LogError("Facility request failed ({FailureType}); correlation {CorrelationId}.",
                exception.GetType().FullName, ApiErrors.Correlation(context));
        return true;
    }
}
