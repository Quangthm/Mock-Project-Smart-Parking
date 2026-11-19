using System.Diagnostics;
using System.Text.Json;

namespace SmartParking.PaymentService.API.Authentication;

internal static class PaymentAuthChallengeWriter
{
    private static readonly JsonSerializerOptions CamelJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string errorCode,
        string message)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        if (statusCode == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.TryAdd("WWW-Authenticate", "Bearer");
        }

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var payload = new
        {
            success = false,
            errorCode,
            message,
            traceId,
            errors = Array.Empty<object>(),
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, CamelJsonOptions),
            context.RequestAborted);
    }
}
