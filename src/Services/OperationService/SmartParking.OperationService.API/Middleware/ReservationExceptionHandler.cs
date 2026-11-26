using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Domain.Reservations;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.API.Middleware;

public sealed class ReservationExceptionHandler(ILogger<ReservationExceptionHandler> logger) : IExceptionHandler
{
    private static readonly IReadOnlyDictionary<Type, Func<Exception, (int Status, string Code, string Message)>> Known =
        new Dictionary<Type, Func<Exception, (int, string, string)>>
        {
            [typeof(ReservationPolicyUnavailableException)] = _ => (503, "RESERVATION_POLICY_UNAVAILABLE", "Reservation policy is not configured consistently."),
            [typeof(DbUpdateConcurrencyException)] = _ => (409, "RESOURCE_VERSION_CONFLICT", "The resource changed; reload its current state."),
            [typeof(InvalidStructureEditException)] = error => (400, "INVALID_STRUCTURE", error.Message),
            [typeof(ReservationGateException)] = error =>
            {
                var gate = (ReservationGateException)error;
                return (gate.StatusCode, gate.Code, gate.Message);
            },
            [typeof(GateTransitionException)] = error => (409, ((GateTransitionException)error).Code, error.Message),
            [typeof(HoldConflictException)] = _ => (409, "STRUCTURE_BUSY", "Another request is managing this site."),
            [typeof(SmartParking.OperationService.Domain.Policies.PolicyOutOfBoundsException)] = error => (400, "POLICY_OUT_OF_BOUNDS", error.Message),
            [typeof(SmartParking.OperationService.Domain.Policies.InvalidPolicyDurationException)] = error => (400, "INVALID_TIME_DURATION", error.Message),
            [typeof(SmartParking.OperationService.Domain.Policies.CrossLotAccessException)] = error => (403, "CROSS_LOT_ACCESS_DENIED", error.Message),
            [typeof(SmartParking.OperationService.Domain.Policies.LotPolicyNotFoundException)] = error => (404, ((SmartParking.OperationService.Domain.Policies.LotPolicyNotFoundException)error).Code, error.Message),
            [typeof(SmartParking.OperationService.Domain.Policies.NoDraftToPublishException)] = error => (409, "NO_DRAFT_TO_PUBLISH", error.Message),
            [typeof(SmartParking.OperationService.Domain.Policies.IdempotencyPayloadMismatchException)] = error => (409, "IDEMPOTENCY_PAYLOAD_MISMATCH", error.Message),
            [typeof(SmartParking.OperationService.Domain.Policies.PolicyOperationInProgressException)] = error => (409, "OPERATION_IN_PROGRESS", error.Message)
        };

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
            return false;
        var result = (Status: 500, Code: "INTERNAL_ERROR", Message: "An unexpected error occurred.");
        object? errors = null;
        if (exception is ValidationException validation)
        {
            var primaryError = validation.Errors.FirstOrDefault(e => e.ErrorCode == "INVALID_TIME_DURATION")
                ?? validation.Errors.FirstOrDefault(e => e.ErrorCode == "INVALID_ACTIVATION_SCHEDULE")
                ?? validation.Errors.FirstOrDefault(e => e.ErrorCode == "POLICY_OUT_OF_BOUNDS")
                ?? validation.Errors.FirstOrDefault();

            var errorCode = primaryError?.ErrorCode;
            var machineCode = errorCode switch
            {
                "POLICY_OUT_OF_BOUNDS" => "POLICY_OUT_OF_BOUNDS",
                "INVALID_TIME_DURATION" => "INVALID_TIME_DURATION",
                "INVALID_ACTIVATION_SCHEDULE" => "INVALID_ACTIVATION_SCHEDULE",
                _ => context.Request.Path.StartsWithSegments("/internal/structure") ? "INVALID_STRUCTURE" : "INVALID_REQUEST"
            };

            result = (400, machineCode, primaryError?.ErrorMessage ?? "Request validation failed.");
            errors = validation.Errors.Select(e => new { property = e.PropertyName, message = e.ErrorMessage });
        }
        else if (exception is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn }
            && (context.Request.Path.StartsWithSegments("/api/reservations") ||
                context.Request.Path.StartsWithSegments("/api/reservation-requests") ||
                context.Request.Path.StartsWithSegments("/api/reservation-terms") ||
                context.Request.Path.StartsWithSegments("/api/reservation-price-previews") ||
                context.Request.Path.StartsWithSegments("/api/me/reservations") ||
                context.Request.Path.StartsWithSegments("/api/parking-sessions")))
            result = (503, "PROTOTYPE_SCHEMA_NOT_READY", "Reservation data is unavailable until database reconciliation is complete.");
        else if (FindMapping(exception.GetType()) is { } map) result = map(exception);
        else if (exception is BadHttpRequestException bad)
            result = (bad.StatusCode, "INVALID_REQUEST", "The request body or parameters are invalid.");
        else if ((exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException)
            is { SqlState: PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.UniqueViolation })
            result = (409, "RESOURCE_CONFLICT", "A conflicting write has already claimed this resource.");
        else logger.LogError("Operations request failed ({FailureType}); correlation {CorrelationId}.",
            exception.GetType().Name, ApiErrors.Correlation(context));

        return await ApiErrors.WriteAsync(context, result.Status, result.Code, result.Message,
            errors, ct: cancellationToken);
    }

    private static Func<Exception, (int Status, string Code, string Message)>? FindMapping(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            if (Known.TryGetValue(current, out var mapping)) return mapping;
        return null;
    }
}
