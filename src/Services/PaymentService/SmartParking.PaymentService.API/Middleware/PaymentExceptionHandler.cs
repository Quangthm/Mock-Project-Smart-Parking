using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

namespace SmartParking.PaymentService.API.Middleware;

public sealed class PaymentExceptionHandler(ILogger<PaymentExceptionHandler> logger) : IExceptionHandler
{
    private static readonly IReadOnlyDictionary<Type,
        Func<Exception, (int Status, string Code, string Message, object[] Errors)>> Mappings =
        new Dictionary<Type, Func<Exception, (int, string, string, object[])>>
        {
            [typeof(ValidationException)] = exception =>
                (400, "VALIDATION_ERROR", "Validation failed for the requested operation.",
                    ((ValidationException)exception).Errors.Select(error => (object)new
                    {
                        field = error.PropertyName, code = error.ErrorCode, message = "The value is invalid."
                    }).ToArray()),
            [typeof(KeyNotFoundException)] = _ => (404, "RESOURCE_NOT_FOUND", "The requested resource was not found.", []),
            [typeof(UnauthorizedAccessException)] = _ => (401, "UNAUTHORIZED", "Authentication is required.", []),
            [typeof(PaymentDomainException)] = exception => MapPayment((PaymentDomainException)exception),
            [typeof(PricingDomainException)] = exception => MapPricing((PricingDomainException)exception)
        };

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (context.Response.HasStarted || ct.IsCancellationRequested ||
            exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return false;

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var result = (Status: 500, Code: "INTERNAL_SERVER_ERROR", Message: "An unexpected internal server error occurred.", Errors: Array.Empty<object>());
        var known = false;
        for (Type? type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (!Mappings.TryGetValue(type, out var map)) continue;
            result = map(exception);
            known = true;
            break;
        }

        var callback = context.Items[typeof(ProcessPayOsWebhookCommand)] as long?;
        if (!known && callback is long started)
            logger.LogError(exception, "PayOS webhook failed with an unconfirmed outcome. TraceId {TraceId}, HTTP {StatusCode}, elapsed {ElapsedMs} ms",
                traceId, result.Status, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        else if (callback is long rejectedAt)
            logger.LogWarning("PayOS webhook completed. TraceId {TraceId}, HTTP {StatusCode}, code {ErrorCode}, elapsed {ElapsedMs} ms",
                traceId, result.Status, result.Code, Stopwatch.GetElapsedTime(rejectedAt).TotalMilliseconds);
        else if (!known)
            logger.LogError(exception, "Payment request failed with an unconfirmed outcome. TraceId {TraceId}, HTTP {StatusCode}",
                traceId, result.Status);

        if (result.Status == 401) context.Response.Headers.TryAdd("WWW-Authenticate", "Bearer");
        context.Response.StatusCode = result.Status;
        if (HttpMethods.IsHead(context.Request.Method)) return true;
        if (callback.HasValue)
        {
            // This provider-facing route has a separate acknowledgement contract.
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                errorCode = result.Code,
                message = result.Message,
                errors = result.Errors,
                traceId,
                timestamp = DateTimeOffset.UtcNow
            }, ct);
            return true;
        }
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            code = result.Code,
            message = result.Message,
            errors = result.Errors,
            traceId
        }, ct);
        return true;
    }

    private static (int, string, string, object[]) MapPayment(PaymentDomainException error)
    {
        var status = error.Code switch
        {
            PaymentDomainException.InvoiceNotFound or PaymentDomainException.OrderNotFound or
            PaymentDomainException.TariffNotFound or "TRANSACTION_NOT_FOUND" or "QUOTE_NOT_FOUND" or
            "COLLECTION_NOT_FOUND" => 404,
            PaymentDomainException.ForbiddenInvoiceAccess or PaymentDomainException.OperatorSiteMismatch or
            PaymentDomainException.CrossTenantAccessDenied or "FORBIDDEN_ORDER_ACCESS" or
            "FORBIDDEN_RESERVATION_DEPOSIT_CREATION" => 403,
            PaymentDomainException.Unauthorized => 401,
            PaymentDomainException.OrderAlreadyPaid or PaymentDomainException.IdempotencyPayloadMismatch or
            "IDEMPOTENCY_CONFLICT" or "IDEMPOTENCY_PAYLOAD_MISMATCH" or "PAYMENT_RETRY_LIMIT_EXCEEDED" or
            "UNKNOWN_TRANSACTION_EXISTS" or "TERMS_STALE" or "QUOTE_EXPIRED" or "QUOTE_CONSUMED" or
            "QUOTE_HOLD_EXPIRED" or "QUOTE_BINDING_CONFLICT" or "QUOTE_STATUS_INVALID" or
            "TRANSACTION_NOT_SETTLED" => 409,
            PaymentDomainException.UnsettledPaymentOrder or PaymentDomainException.OrderExpired => 422,
            "QUOTE_PRICING_UNAVAILABLE" => 503,
            "GATEWAY_TIMEOUT" => 504,
            _ => 400
        };
        return (status, error.Code, SafeMessage(status), FieldErrors(error.Field));
    }

    private static (int, string, string, object[]) MapPricing(PricingDomainException error)
    {
        var status = error.Code switch
        {
            "TARIFF_NOT_FOUND" => 404,
            "CORRUPTED_POLICY_SNAPSHOT" or "INVALID_TIME_RANGE" => 422,
            "PARKING_SERVICE_UNAVAILABLE" or "RESERVATION_SERVICE_UNAVAILABLE" => 503,
            "PARKING_SERVICE_TIMEOUT" or "RESERVATION_SERVICE_TIMEOUT" => 504,
            _ => 400
        };
        return (status, error.Code, SafeMessage(status), FieldErrors(error.Field));
    }

    private static object[] FieldErrors(string? field) =>
        string.IsNullOrEmpty(field) ? [] : [new { field, message = "The value is invalid." }];

    private static string SafeMessage(int status) => status switch
    {
        401 => "Authentication is required.",
        403 => "Access to the requested operation was denied.",
        404 => "The requested resource was not found.",
        409 => "The operation conflicts with the current payment state.",
        422 => "The operation cannot be completed in the current payment state.",
        503 => "A required service is unavailable.",
        504 => "A required service did not respond in time.",
        _ => "The payment request could not be accepted."
    };
}
