using System.Collections.Frozen;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.API.Common;

/// <summary>
/// Immutable, exact-runtime-type exception mapper with explicit PostgreSQL conflict handling
/// and sanitized provider/unexpected error fallbacks.
/// </summary>
public sealed class PaymentExceptionMapper
{
    private readonly FrozenDictionary<Type, Func<Exception, PaymentErrorHandlingResult>> _mappings;

    public PaymentExceptionMapper(IEnumerable<KeyValuePair<Type, Func<Exception, PaymentErrorHandlingResult>>> registrations)
    {
        var dictionary = new Dictionary<Type, Func<Exception, PaymentErrorHandlingResult>>();
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

    public PaymentErrorHandlingResult Map(Exception exception)
    {
        if (_mappings.TryGetValue(exception.GetType(), out var mapping))
        {
            return mapping(exception);
        }

        if ((exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException)
            is { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation })
        {
            return new(PaymentErrorResponse.Error(
                StatusCodes.Status409Conflict,
                "RESOURCE_CONFLICT",
                "A conflicting write has already claimed this resource."));
        }

        return Unexpected();
    }

    public static PaymentErrorHandlingResult Unexpected() =>
        new(PaymentErrorResponse.Error(
            StatusCodes.Status500InternalServerError,
            "INTERNAL_SERVER_ERROR",
            "An unexpected internal server error occurred."),
            IsUnexpected: true);

    public static PaymentExceptionMapper CreateDefault() =>
        new(new Dictionary<Type, Func<Exception, PaymentErrorHandlingResult>>
        {
            [typeof(ValidationException)] = exception => new(PaymentErrorResponse.ValidationError(
                ((ValidationException)exception).Errors.Select(error =>
                    new PaymentErrorDetail(error.PropertyName, error.ErrorMessage, error.ErrorCode)))),
            [typeof(PaymentDomainException)] = exception => MapPaymentDomainException((PaymentDomainException)exception),
            [typeof(PricingDomainException)] = exception => MapPricingDomainException((PricingDomainException)exception),
            [typeof(DbUpdateConcurrencyException)] = _ => new(PaymentErrorResponse.Error(
                StatusCodes.Status409Conflict,
                "RESOURCE_VERSION_CONFLICT",
                "The payment resource changed concurrently. Reload and retry.")),
            [typeof(BadHttpRequestException)] = exception =>
                PaymentErrorResponse.TryForStatus(((BadHttpRequestException)exception).StatusCode, out var response)
                    ? new(response)
                    : Unexpected()
        });

    private static PaymentErrorHandlingResult MapPaymentDomainException(PaymentDomainException exception)
    {
        if (string.Equals(exception.Code, "GATEWAY_TIMEOUT", StringComparison.Ordinal))
        {
            return new(PaymentErrorResponse.Error(
                StatusCodes.Status504GatewayTimeout,
                exception.Code,
                "The payment gateway timed out. Check the payment status before retrying."));
        }

        if (string.Equals(exception.Code, "PAYOS_CREDENTIALS_NOT_CONFIGURED", StringComparison.Ordinal))
        {
            return new(PaymentErrorResponse.Error(
                StatusCodes.Status503ServiceUnavailable,
                exception.Code,
                "The payment gateway is temporarily unavailable."));
        }

        if (string.Equals(exception.Code, "PAYOS_CHECKOUT_CANCELLATION_FAILED", StringComparison.Ordinal) ||
            string.Equals(exception.Code, "GATEWAY_INITIATION_FAILED", StringComparison.Ordinal) ||
            (exception.Code.StartsWith("PAYOS_", StringComparison.Ordinal) &&
             !string.Equals(exception.Code, "PAYOS_CHECKOUT_LINK_UNAVAILABLE", StringComparison.Ordinal)))
        {
            return new(PaymentErrorResponse.Error(
                StatusCodes.Status502BadGateway,
                exception.Code,
                "The payment gateway could not complete the request. Please try again later."));
        }

        var statusCode = exception.Code switch
        {
            PaymentDomainException.InvoiceNotFound or
            PaymentDomainException.OrderNotFound or
            PaymentDomainException.TariffNotFound or
            "TRANSACTION_NOT_FOUND" or
            "QUOTE_NOT_FOUND" or
            "COLLECTION_NOT_FOUND" => StatusCodes.Status404NotFound,

            PaymentDomainException.Unauthorized => StatusCodes.Status401Unauthorized,

            PaymentDomainException.ForbiddenInvoiceAccess or
            PaymentDomainException.OperatorSiteMismatch or
            PaymentDomainException.CrossTenantAccessDenied or
            "FORBIDDEN_ORDER_ACCESS" or
            "FORBIDDEN_RESERVATION_DEPOSIT_CREATION" => StatusCodes.Status403Forbidden,

            PaymentDomainException.OrderAlreadyPaid or
            "ORDER_ALREADY_PAID_ONLINE" or
            "ORDER_INVALID_STATUS" or
            PaymentDomainException.IdempotencyPayloadMismatch or
            "IDEMPOTENCY_CONFLICT" or
            "PAYMENT_RETRY_LIMIT_EXCEEDED" or
            "PAYMENT_TRANSACTION_AMBIGUOUS" or
            "UNKNOWN_TRANSACTION_EXISTS" or
            "PAYOS_CHECKOUT_LINK_UNAVAILABLE" or
            "TERMS_STALE" or
            "QUOTE_EXPIRED" or
            "QUOTE_STATUS_INVALID" or
            "TRANSACTION_NOT_SETTLED" => StatusCodes.Status409Conflict,

            PaymentDomainException.UnsettledPaymentOrder or
            PaymentDomainException.OrderExpired or
            "ZERO_AMOUNT_ORDER" or
            "INVALID_ORDER_CODE" => StatusCodes.Status422UnprocessableEntity,

            PaymentDomainException.InvalidCutoffTime or
            "INVALID_WEBHOOK_SIGNATURE" or
            "QUOTE_ACCOUNT_MISMATCH" or
            "QUOTE_AMOUNT_MISMATCH" or
            "QUOTE_TENANT_MISMATCH" or
            "QUOTE_SITE_MISMATCH" or
            "QUOTE_CURRENCY_MISMATCH" or
            "INVALID_AMOUNT" or
            "INVALID_TEST_AMOUNT" or
            "UNSUPPORTED_VEHICLE_TYPE" => StatusCodes.Status400BadRequest,

            _ => StatusCodes.Status400BadRequest
        };

        var errors = string.IsNullOrEmpty(exception.Field)
            ? null
            : new[] { new PaymentErrorDetail(exception.Field, exception.Message) };

        return new(PaymentErrorResponse.Error(statusCode, exception.Code, exception.Message, errors));
    }

    private static PaymentErrorHandlingResult MapPricingDomainException(PricingDomainException exception)
    {
        var statusCode = exception.Code switch
        {
            "TARIFF_NOT_FOUND" => StatusCodes.Status404NotFound,
            "CORRUPTED_POLICY_SNAPSHOT" or
            "INVALID_TIME_RANGE" or
            "VEHICLE_TYPE_MISMATCH" or
            "INVALID_PRICING_POLICY" or
            "INVALID_TIMEZONE" or
            "OVERLAPPING_TIME_BLOCKS" or
            "UNCOVERED_TIME_BLOCKS" => StatusCodes.Status422UnprocessableEntity,
            "PARKING_SERVICE_UNAVAILABLE" or "RESERVATION_SERVICE_UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
            "PARKING_SERVICE_TIMEOUT" or "RESERVATION_SERVICE_TIMEOUT" => StatusCodes.Status504GatewayTimeout,
            "UNSUPPORTED_VEHICLE_TYPE" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        var errors = string.IsNullOrEmpty(exception.Field)
            ? null
            : new[] { new PaymentErrorDetail(exception.Field, exception.Message) };

        return new(PaymentErrorResponse.Error(statusCode, exception.Code, exception.Message, errors));
    }
}
