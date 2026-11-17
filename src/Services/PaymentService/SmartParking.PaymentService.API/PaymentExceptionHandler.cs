using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.API;

/// <summary>
/// Framework <see cref="IExceptionHandler"/> integration (coding skill §7). Preserves the
/// established payment error envelope and the HTTP-status mapping that previously lived in
/// the custom catch-all middleware, so API consumers see no response-contract change.
/// </summary>
public sealed class PaymentExceptionHandler : IExceptionHandler
{
    private const string AdminPaymentPathPrefix = "/api/v1/admin/integrations/payment";

    private readonly ILogger<PaymentExceptionHandler> _logger;

    public PaymentExceptionHandler(ILogger<PaymentExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // If the response has already started there is nothing safe to rewrite.
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var response = BuildResponse(httpContext, exception);

        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = response.StatusCode;

        var responsePayload = new
        {
            success = false,
            code = response.ErrorCode,
            errorCode = response.ErrorCode,
            message = response.Message,
            traceId = response.TraceId,
            errors = response.Errors,
            timestamp = response.Timestamp
        };

        var json = JsonSerializer.Serialize(responsePayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await httpContext.Response.WriteAsync(json, cancellationToken);
        return true;
    }

    private ErrorResponse BuildResponse(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var timestamp = DateTimeOffset.UtcNow;

        int statusCode;
        string errorCode;
        string message;
        var errors = new List<object>();

        switch (exception)
        {
            case PaymentIntegrationPersistenceException:
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                errorCode = PaymentIntegrationPersistenceException.ErrorCode;
                message = "Payment integration configuration is temporarily unavailable. Retry the request.";
                break;
            case PaymentIntegrationException pie:
                statusCode = pie.Code switch
                {
                    "INVALID_PAYMENT_BASE_URL" => (int)HttpStatusCode.BadRequest,
                    "INVALID_CREDENTIALS" => (int)HttpStatusCode.BadRequest,
                    "VALIDATION_FAILED" => (int)HttpStatusCode.BadRequest,
                    _ => (int)HttpStatusCode.BadRequest
                };
                errorCode = pie.Code;
                message = pie.Message;
                if (pie.Errors is not null)
                {
                    foreach (var err in pie.Errors)
                    {
                        errors.Add(new
                        {
                            property = err.Property,
                            field = err.Property,
                            message = err.Message
                        });
                    }
                }
                break;

            case ValidationException validationEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                var isAdminPaymentPath = context.Request.Path.StartsWithSegments(AdminPaymentPathPrefix);
                errorCode = isAdminPaymentPath ? "VALIDATION_FAILED" : "VALIDATION_ERROR";
                message = isAdminPaymentPath
                    ? "The request payload failed one or more validation rules."
                    : "Validation failed for the requested operation.";
                foreach (var err in validationEx.Errors)
                {
                    errors.Add(new
                    {
                        property = err.PropertyName,
                        field = err.PropertyName,
                        code = err.ErrorCode,
                        message = err.ErrorMessage
                    });
                }
                break;

            case BadHttpRequestException badRequestEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                errorCode = "BAD_REQUEST";
                message = "The request could not be processed because the payload or format is invalid.";
                _ = badRequestEx;
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

            case PaymentDomainException paymentEx:
                statusCode = paymentEx.Code switch
                {
                    PaymentDomainException.InvoiceNotFound or
                    PaymentDomainException.OrderNotFound or
                    PaymentDomainException.TariffNotFound or
                    "TRANSACTION_NOT_FOUND" or
                    "QUOTE_NOT_FOUND" or
                    "COLLECTION_NOT_FOUND" => (int)HttpStatusCode.NotFound,

                    PaymentDomainException.ForbiddenInvoiceAccess or
                    PaymentDomainException.OperatorSiteMismatch or
                    PaymentDomainException.CrossTenantAccessDenied or
                    "FORBIDDEN_ORDER_ACCESS" or
                    "FORBIDDEN_RESERVATION_DEPOSIT_CREATION" => (int)HttpStatusCode.Forbidden,

                    PaymentDomainException.Unauthorized => (int)HttpStatusCode.Unauthorized,

                    PaymentDomainException.OrderAlreadyPaid or
                    PaymentDomainException.IdempotencyPayloadMismatch or
                    "IDEMPOTENCY_CONFLICT" or
                    "IDEMPOTENCY_PAYLOAD_MISMATCH" or
                    "PAYMENT_RETRY_LIMIT_EXCEEDED" or
                    "UNKNOWN_TRANSACTION_EXISTS" or
                    "TERMS_STALE" or
                    "QUOTE_EXPIRED" or
                    "QUOTE_STATUS_INVALID" or
                    "TRANSACTION_NOT_SETTLED" => (int)HttpStatusCode.Conflict,

                    PaymentDomainException.UnsettledPaymentOrder or
                    PaymentDomainException.OrderExpired => (int)HttpStatusCode.UnprocessableEntity,

                    PaymentDomainException.InvalidCutoffTime or
                    "INVALID_WEBHOOK_SIGNATURE" or
                    "QUOTE_ACCOUNT_MISMATCH" or
                    "QUOTE_AMOUNT_MISMATCH" => (int)HttpStatusCode.BadRequest,

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

        return new ErrorResponse(statusCode, errorCode, message, traceId, errors, timestamp);
    }

    private sealed record ErrorResponse(
        int StatusCode,
        string ErrorCode,
        string Message,
        string TraceId,
        List<object> Errors,
        DateTimeOffset Timestamp);
}
