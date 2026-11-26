using System.Collections.Frozen;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.API.Common;

/// <summary>
/// Immutable, exact-runtime-type exception mapper for Notification, Audit, and Support modules
/// with sanitized authorization/not-found/database/unexpected responses.
/// </summary>
public sealed class NotificationAuditExceptionMapper
{
    private readonly FrozenDictionary<Type, Func<Exception, NotificationErrorHandlingResult>> _mappings;

    public NotificationAuditExceptionMapper(
        IEnumerable<KeyValuePair<Type, Func<Exception, NotificationErrorHandlingResult>>> registrations)
    {
        var dictionary = new Dictionary<Type, Func<Exception, NotificationErrorHandlingResult>>();
        foreach (var registration in registrations)
        {
            if (!typeof(Exception).IsAssignableFrom(registration.Key))
            {
                throw new ArgumentException("Only exception types can be registered.", nameof(registrations));
            }

            ArgumentNullException.ThrowIfNull(registration.Value);
            if (!dictionary.TryAdd(registration.Key, registration.Value))
            {
                throw new ArgumentException($"Duplicate exception mapping: {registration.Key.Name}.", nameof(registrations));
            }
        }

        _mappings = dictionary.ToFrozenDictionary();
    }

    public NotificationErrorHandlingResult Map(Exception exception)
    {
        if (_mappings.TryGetValue(exception.GetType(), out var mapping))
        {
            return mapping(exception);
        }

        if ((exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException)
            is { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation })
        {
            return new(NotificationErrorResponse.Error(
                StatusCodes.Status409Conflict,
                "RESOURCE_CONFLICT",
                "A conflicting write has already claimed this resource."));
        }

        return Unexpected();
    }

    public static NotificationErrorHandlingResult Unexpected() =>
        new(NotificationErrorResponse.Error(
            StatusCodes.Status500InternalServerError,
            "INTERNAL_SERVER_ERROR",
            "An unexpected internal server error occurred."),
            IsUnexpected: true);

    public static NotificationAuditExceptionMapper CreateDefault() =>
        new(new Dictionary<Type, Func<Exception, NotificationErrorHandlingResult>>
        {
            [typeof(ValidationException)] = exception => new(NotificationErrorResponse.ValidationError(
                ((ValidationException)exception).Errors.Select(error =>
                    new NotificationErrorDetail(error.PropertyName, error.ErrorMessage, error.ErrorCode)))),
            [typeof(NotificationDomainException)] = exception => new(NotificationErrorResponse.Error(
                StatusCodes.Status400BadRequest,
                "DOMAIN_ERROR",
                exception.Message)),
            [typeof(NotificationForbiddenException)] = _ => new(NotificationErrorResponse.Error(
                StatusCodes.Status403Forbidden,
                "FORBIDDEN",
                "You do not have permission to access this resource.")),
            [typeof(KeyNotFoundException)] = _ => new(NotificationErrorResponse.Error(
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                "The requested resource was not found.")),
            [typeof(DbUpdateConcurrencyException)] = _ => new(NotificationErrorResponse.Error(
                StatusCodes.Status409Conflict,
                "RESOURCE_VERSION_CONFLICT",
                "The resource changed concurrently. Reload and retry.")),
            [typeof(BadHttpRequestException)] = exception =>
            {
                var status = ((BadHttpRequestException)exception).StatusCode;
                if (NotificationErrorResponse.TryForStatus(status, out var response))
                {
                    return new(response);
                }

                return status is >= 400 and <= 499
                    ? new(NotificationErrorResponse.Error(status, "INVALID_REQUEST", "The request is invalid."))
                    : Unexpected();
            }
        });
}
