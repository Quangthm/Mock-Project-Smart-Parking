using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using SmartParking.PaymentService.API.Common;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.API.Middleware;

/// <summary>Singleton handler; all dependencies are immutable or stateless.</summary>
public sealed class PaymentExceptionHandler : IExceptionHandler
{
    private readonly PaymentExceptionMapper _mapper;
    private readonly PaymentApiErrorWriter _writer;
    private readonly ILogger<PaymentExceptionHandler> _logger;
    private readonly bool _useLegacyContract;

    public PaymentExceptionHandler(
        PaymentExceptionMapper mapper,
        PaymentApiErrorWriter writer,
        ILogger<PaymentExceptionHandler> logger)
    {
        _mapper = mapper;
        _writer = writer;
        _logger = logger;
        _useLegacyContract = false;
    }

    public PaymentExceptionHandler(ILogger<PaymentExceptionHandler> logger)
        : this(PaymentExceptionMapper.CreateDefault(), new PaymentApiErrorWriter(), logger)
    {
        _useLegacyContract = true;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted ||
            cancellationToken.IsCancellationRequested ||
            (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested))
        {
            return false;
        }

        var result = _mapper.Map(exception);
        var callbackStartedAt = httpContext.Items[typeof(ProcessPayOsWebhookCommand)] as long?;

        if (callbackStartedAt.HasValue)
        {
            await WriteWebhookAcknowledgementAsync(httpContext, result, exception, callbackStartedAt.Value, cancellationToken);
            return true;
        }

        if (_useLegacyContract)
        {
            await WriteLegacyContractAsync(httpContext, result, exception, cancellationToken);
            return true;
        }

        if (!await _writer.TryWriteAsync(httpContext, result.Response, cancellationToken))
        {
            return false;
        }

        if (result.IsUnexpected)
        {
            // Log only exception type and server-generated correlation ID to avoid leaking SQL, PII, or provider secrets.
            _logger.LogError(
                "Payment request failed with {ExceptionType}; correlation {CorrelationId}.",
                exception.GetType().FullName,
                PaymentRequestCorrelation.Get(httpContext));
        }

        return true;
    }

    private async Task WriteWebhookAcknowledgementAsync(
        HttpContext httpContext,
        PaymentErrorHandlingResult result,
        Exception exception,
        long startedAt,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        if (result.IsUnexpected)
        {
            _logger.LogError(
                exception,
                "PayOS webhook failed with an unconfirmed outcome. TraceId {TraceId}, HTTP {StatusCode}, elapsed {ElapsedMs} ms",
                traceId,
                result.Response.StatusCode,
                elapsedMs);
        }
        else
        {
            _logger.LogWarning(
                "PayOS webhook completed. TraceId {TraceId}, HTTP {StatusCode}, code {ErrorCode}, elapsed {ElapsedMs} ms",
                traceId,
                result.Response.StatusCode,
                result.Response.Code,
                elapsedMs);
        }

        httpContext.Response.StatusCode = result.Response.StatusCode;
        if (HttpMethods.IsHead(httpContext.Request.Method))
        {
            return;
        }

        var errors = (result.Response.Errors ?? Array.Empty<PaymentErrorDetail>())
            .Select(e => (object)new { field = e.Field, message = e.Message })
            .ToArray();

        await httpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            errorCode = result.Response.Code,
            message = SanitizeLegacyMessage(result.Response, exception),
            errors,
            traceId,
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private async Task WriteLegacyContractAsync(
        HttpContext httpContext,
        PaymentErrorHandlingResult result,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var code = result.Response.Code == "RESOURCE_CONFLICT"
            ? "IDEMPOTENCY_CONFLICT"
            : result.Response.Code;

        if (result.IsUnexpected)
        {
            _logger.LogError(
                exception,
                "Payment request failed with an unconfirmed outcome. TraceId {TraceId}, HTTP {StatusCode}",
                traceId,
                result.Response.StatusCode);
        }

        if (result.Response.StatusCode == StatusCodes.Status401Unauthorized)
        {
            httpContext.Response.Headers.TryAdd("WWW-Authenticate", "Bearer");
        }

        httpContext.Response.StatusCode = result.Response.StatusCode;
        if (HttpMethods.IsHead(httpContext.Request.Method))
        {
            return;
        }

        var errors = (result.Response.Errors ?? Array.Empty<PaymentErrorDetail>())
            .Select(e => (object)new { field = e.Field, message = "The value is invalid." })
            .ToArray();

        await httpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            code,
            message = SanitizeLegacyMessage(result.Response, exception),
            errors,
            traceId
        }, cancellationToken);
    }

    private static string SanitizeLegacyMessage(PaymentErrorResponse response, Exception exception)
    {
        if (exception is not (PaymentDomainException or PricingDomainException))
        {
            return response.Message;
        }

        return response.StatusCode switch
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
}
