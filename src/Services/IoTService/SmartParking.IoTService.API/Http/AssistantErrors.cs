using Microsoft.AspNetCore.Diagnostics;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Demo;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.API.Http;

public sealed record AssistantError(int Status, string Code, string Message);

public static class AssistantErrors
{
    // Exact HTTP status matching; unknown statuses use a sanitized fallback.
    private static readonly IReadOnlyDictionary<int, AssistantError> Statuses = new Dictionary<int, AssistantError>
    {
        [400] = new(400, "VALIDATION_FAILED", "The request is invalid."),
        [401] = new(401, "INVALID_TOKEN", "A valid access token is required."),
        [403] = new(403, "ASSISTANT_FORBIDDEN", "This account cannot use the assistant."),
        [404] = new(404, "RESOURCE_NOT_FOUND", "The resource was not found."),
        [405] = new(405, "METHOD_NOT_ALLOWED", "The request method is not supported."),
        [413] = new(413, "PAYLOAD_TOO_LARGE", "The request body is too large."),
        [415] = new(415, "UNSUPPORTED_MEDIA_TYPE", "The content type is not supported."),
        [429] = new(429, "RATE_LIMITED", "Please retry later."),
        [500] = new(500, "INTERNAL_ERROR", "An unexpected error occurred.")
    };

    public static AssistantError ForStatus(int status) => Statuses.GetValueOrDefault(status)
        ?? new(status, "INTERNAL_ERROR", "The request could not be completed.");

    public static ErrorEnvelope Envelope(HttpContext context, AssistantError error) =>
        new(false, error.Code, error.Message, [], context.TraceIdentifier);

    public static async Task WriteAsync(HttpContext context, AssistantError error)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = error.Status;
        if (error.Status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        if (HttpMethods.IsHead(context.Request.Method) || error.Status == 204) return;
        await context.Response.WriteAsJsonAsync(Envelope(context, error), context.RequestAborted);
    }
}

public sealed class AssistantExceptionHandler(ILogger<AssistantExceptionHandler> logger) : IExceptionHandler
{
    // Exact type matching is intentional. Unknown subclasses use the safe fallback.
    private static readonly IReadOnlyDictionary<Type, Func<Exception, AssistantError>> Mappings =
        new Dictionary<Type, Func<Exception, AssistantError>>
        {
            [typeof(ClientUpgradeRequiredException)] = _ => new(409, "CLIENT_UPGRADE_REQUIRED", "Upgrade the settings client before changing models."),
            [typeof(ProviderDiagnosticsException)] = ex => new(((ProviderDiagnosticsException)ex).Status, ((ProviderDiagnosticsException)ex).Code, "The provider operation could not be completed."),
            [typeof(ManagementValidationException)] = _ => AssistantErrors.ForStatus(400),
            [typeof(ManagementConflictException)] = ex => new(409, ((ManagementConflictException)ex).Idempotency ? "IDEMPOTENCY_CONFLICT" : "REVISION_CONFLICT", "Reload settings before making another change."),
            [typeof(ManagementUnavailableException)] = _ => new(503, "MANAGEMENT_UNAVAILABLE", "Assistant management is not configured or unavailable."),
            [typeof(AssistantMaintenanceException)] = ex => new(503, "ASSISTANT_MAINTENANCE", ex.Message),
            [typeof(DemoGroundedUnavailableException)] = ex => ModelFailure((DemoGroundedUnavailableException)ex),
            [typeof(DemoMessageValidationException)] = _ => AssistantErrors.ForStatus(400),
            [typeof(AssistantAccessException)] = ex => AssistantErrors.ForStatus(((AssistantAccessException)ex).Authenticated ? 403 : 401),
            [typeof(BadHttpRequestException)] = ex => AssistantErrors.ForStatus(((BadHttpRequestException)ex).StatusCode)
        };

    // Public categories use the established generation vocabulary. Details remain static;
    // no upstream body, key, arbitrary exception text or internal status is serialized.
    private static AssistantError ModelFailure(DemoGroundedUnavailableException error) => error.Failure switch
    {
        DemoGroundedFailure.Knowledge => new(503, "RETRIEVAL_UNAVAILABLE", "The assistant knowledge could not be loaded. Contact an administrator."),
        DemoGroundedFailure.Configuration => new(503, "ASSISTANT_NOT_CONFIGURED", "The assistant model is not configured. Contact an administrator."),
        DemoGroundedFailure.Timeout => new(503, "MODEL_TIMEOUT", "The AI service did not respond in time. Please try again later."),
        DemoGroundedFailure.InvalidResponse or DemoGroundedFailure.ResponseTooLarge => new(503, "INVALID_MODEL_OUTPUT", "The AI service returned an unusable response. Please try again later."),
        DemoGroundedFailure.Busy => new(503, "MODEL_UNAVAILABLE", "The assistant is busy. Wait before sending another message."),
        DemoGroundedFailure.Network => new(503, "MODEL_UNAVAILABLE", "The AI service could not be reached. Please try again later."),
        DemoGroundedFailure.ProviderRejected when error.ProviderStatus == 429 => new(503, "MODEL_UNAVAILABLE", "The AI provider is rate limited. Wait before trying again."),
        DemoGroundedFailure.ProviderRejected when error.ProviderStatus is 400 or 401 or 403 or 404 => new(503, "MODEL_UNAVAILABLE", "The AI provider rejected the request. Check the provider configuration."),
        DemoGroundedFailure.ProviderRejected => new(503, "MODEL_UNAVAILABLE", "The AI provider is temporarily unavailable. Please try again later."),
        _ => new(503, "MODEL_UNAVAILABLE", "The demo model or knowledge is unavailable.")
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested) return false;
        var known = Mappings.TryGetValue(exception.GetType(), out var map);
        var error = known ? map!(exception) : AssistantErrors.ForStatus(500);
        // Do not log raw exceptions: request values, tokens or provider payloads may occur in messages.
        if (!known) logger.LogError("Assistant request failed. TraceId: {TraceId}; ExceptionType: {ExceptionType}",
            context.TraceIdentifier, exception.GetType().Name);
        if (exception is DemoGroundedUnavailableException modelFailure)
            logger.LogWarning("Assistant model unavailable. Failure: {Failure}; ProviderStatus: {ProviderStatus}; TraceId: {TraceId}",
                modelFailure.Failure, modelFailure.ProviderStatus, context.TraceIdentifier);
        if (error.Status == 429) context.Response.Headers.RetryAfter = "60";
        await AssistantErrors.WriteAsync(context, error);
        return true;
    }
}
