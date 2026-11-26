using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.API.Middleware;

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
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var timestamp = DateTimeOffset.UtcNow;

        int statusCode;
        string errorCode;
        string message;
        List<object> errors = new();

        switch (exception)
        {
            case ValidationException validationEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                var passValidationCode = validationEx.Errors
                    .Select(e => e.ErrorCode)
                    .FirstOrDefault(c => c is "INVALID_FINANCIAL_TERMS" or "INVALID_CLOSURE_INTERVALS" or "INVALID_PRODUCT_TYPE");
                errorCode = passValidationCode ?? "VALIDATION_ERROR";
                message = passValidationCode is not null
                    ? (validationEx.Errors.FirstOrDefault()?.ErrorMessage ?? "Validation failed for the requested operation.")
                    : "Validation failed for the requested operation.";
                foreach (var err in validationEx.Errors)
                {
                    errors.Add(new
                    {
                        field = err.PropertyName,
                        code = err.ErrorCode,
                        message = err.ErrorMessage
                    });
                }
                break;

            case KeyNotFoundException keyNotFoundEx:
                statusCode = (int)HttpStatusCode.NotFound;
                errorCode = "RESOURCE_NOT_FOUND";
                message = keyNotFoundEx.Message;
                break;

            case UnauthorizedAccessException unauthEx:
                statusCode = (int)HttpStatusCode.Unauthorized;
                errorCode = "UNAUTHORIZED";
                message = unauthEx.Message;
                break;

            case PaymentJwksUnavailableException jwksEx:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                errorCode = "AUTHENTICATION_KEYS_UNAVAILABLE";
                message = jwksEx.Message;
                break;

            case DbUpdateException dbEx when dbEx.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }:
            case PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }:
                statusCode = (int)HttpStatusCode.Conflict;
                errorCode = "IDEMPOTENCY_CONFLICT";
                message = "A conflicting request or duplicate idempotency key already exists.";
                break;

            case PaymentDomainException paymentEx:
                statusCode = paymentEx.Code switch
                {
                    PaymentDomainException.InvoiceNotFound or
                    PaymentDomainException.OrderNotFound or
                    PaymentDomainException.TariffNotFound or
                    "TRANSACTION_NOT_FOUND" or
                    "QUOTE_NOT_FOUND" or
                    "COLLECTION_NOT_FOUND" or
                    "SITE_NOT_FOUND" or
                    "FINANCIAL_TERMS_NOT_FOUND" => (int)HttpStatusCode.NotFound,

                    PaymentDomainException.ForbiddenInvoiceAccess or
                    PaymentDomainException.OperatorSiteMismatch or
                    PaymentDomainException.CrossTenantAccessDenied or
                    "FORBIDDEN" or
                    "FORBIDDEN_SITE_SCOPE" or
                    "FORBIDDEN_ORDER_ACCESS" or
                    "FORBIDDEN_RESERVATION_DEPOSIT_CREATION" => (int)HttpStatusCode.Forbidden,

                    PaymentDomainException.Unauthorized or
                    "UNAUTHENTICATED" => (int)HttpStatusCode.Unauthorized,

                    PaymentDomainException.OrderAlreadyPaid or
                    PaymentDomainException.IdempotencyPayloadMismatch or
                    "IDEMPOTENCY_CONFLICT" or
                    "IDEMPOTENCY_PAYLOAD_MISMATCH" or
                    "PAYMENT_RETRY_LIMIT_EXCEEDED" or
                    "UNKNOWN_TRANSACTION_EXISTS" or
                    "TERMS_STALE" or
                    "FINANCIAL_TERMS_CHANGED" or
                    "PLAN_VERSION_CONFLICT" or
                    "QUOTE_EXPIRED" or
                    "QUOTE_STATUS_INVALID" or
                    "TRANSACTION_NOT_SETTLED" => (int)HttpStatusCode.Conflict,

                    PaymentDomainException.UnsettledPaymentOrder or
                    PaymentDomainException.OrderExpired or
                    "YEARLY_POLICY_BLOCKED_Q05" or
                    "FINANCIAL_TERMS_MISSING" or
                    "CLOSURE_COMPENSATION_EXCEEDS_ORDER" or
                    "ORDER_NOT_PAID" or
                    "ORDER_SCOPE_MISMATCH" or
                    "PASS_ORDER_MISMATCH" => (int)HttpStatusCode.UnprocessableEntity,

                    PaymentDomainException.InvalidCutoffTime or
                    "INVALID_WEBHOOK_SIGNATURE" or
                    "INVALID_FINANCIAL_TERMS" or
                    "INVALID_CLOSURE_INTERVALS" or
                    "INVALID_PRODUCT_TYPE" or
                    "QUOTE_ACCOUNT_MISMATCH" or
                    "QUOTE_AMOUNT_MISMATCH" => (int)HttpStatusCode.BadRequest,

                    "IDENTITY_UNAVAILABLE" or
                    "FACILITY_UNAVAILABLE" or
                    "AUTHENTICATION_KEYS_UNAVAILABLE" => (int)HttpStatusCode.ServiceUnavailable,

                    "GATEWAY_TIMEOUT" => (int)HttpStatusCode.GatewayTimeout,
                    _ => (int)HttpStatusCode.BadRequest
                };
                errorCode = paymentEx.Code;
                message = paymentEx.Message;
                if (!string.IsNullOrEmpty(paymentEx.Field))
                {
                    errors.Add(new
                    {
                        field = paymentEx.Field,
                        message = paymentEx.Message
                    });
                }
                break;

            case PricingDomainException domainEx:
                statusCode = domainEx.Code switch
                {
                    "TARIFF_NOT_FOUND" => (int)HttpStatusCode.NotFound,
                    "CORRUPTED_POLICY_SNAPSHOT" => (int)HttpStatusCode.UnprocessableEntity,
                    "UNSUPPORTED_VEHICLE_TYPE" => (int)HttpStatusCode.BadRequest,
                    "INVALID_TIME_RANGE" => (int)HttpStatusCode.UnprocessableEntity,
                    "PARKING_SERVICE_UNAVAILABLE" or "RESERVATION_SERVICE_UNAVAILABLE" => (int)HttpStatusCode.ServiceUnavailable,
                    "PARKING_SERVICE_TIMEOUT" or "RESERVATION_SERVICE_TIMEOUT" => (int)HttpStatusCode.GatewayTimeout,
                    _ => (int)HttpStatusCode.BadRequest
                };
                errorCode = domainEx.Code;
                message = domainEx.Message;
                if (!string.IsNullOrEmpty(domainEx.Field))
                {
                    errors.Add(new
                    {
                        field = domainEx.Field,
                        message = domainEx.Message
                    });
                }
                break;

            default:
                _logger.LogError(exception, "Unhandled exception occurred");
                statusCode = (int)HttpStatusCode.InternalServerError;
                errorCode = "INTERNAL_SERVER_ERROR";
                message = "An unexpected internal server error occurred.";
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var responsePayload = new
        {
            success = false,
            errorCode,
            message,
            traceId,
            errors,
            timestamp
        };

        var json = JsonSerializer.Serialize(responsePayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }
}
