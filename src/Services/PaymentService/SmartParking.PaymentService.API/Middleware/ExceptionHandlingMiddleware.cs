using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentValidation;
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
                errorCode = "VALIDATION_ERROR";
                message = "Validation failed for the requested operation.";
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

            case PaymentDomainException paymentEx:
                statusCode = paymentEx.Code switch
                {
                    "ORDER_NOT_FOUND" or "TRANSACTION_NOT_FOUND" => (int)HttpStatusCode.NotFound,
                    "ORDER_ALREADY_PAID" or "IDEMPOTENCY_CONFLICT" or "PAYMENT_RETRY_LIMIT_EXCEEDED" => (int)HttpStatusCode.Conflict,
                    "ORDER_EXPIRED" => (int)HttpStatusCode.UnprocessableEntity,
                    "INVALID_WEBHOOK_SIGNATURE" => (int)HttpStatusCode.BadRequest,
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
