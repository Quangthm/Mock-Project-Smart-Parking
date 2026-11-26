namespace SmartParking.NotificationAuditService.API.Common;

/// <summary>Writes the approved error envelope without duplicating HTTP status in JSON.</summary>
public sealed class NotificationApiErrorWriter
{
    public async ValueTask<bool> TryWriteAsync(
        HttpContext context,
        NotificationErrorResponse response,
        CancellationToken cancellationToken = default)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentLength = null;

        if (context.Response.Headers.CacheControl.Count == 0)
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        if (response.StatusCode == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.TryAdd("WWW-Authenticate", "Bearer");
        }

        if (HttpMethods.IsHead(context.Request.Method) ||
            response.StatusCode is StatusCodes.Status204NoContent or StatusCodes.Status304NotModified)
        {
            return true;
        }

        var body = new Dictionary<string, object?>
        {
            ["success"] = false,
            ["code"] = response.Code,
            ["errorCode"] = response.Code,
            ["message"] = response.Message,
            ["traceId"] = NotificationRequestCorrelation.Get(context)
        };

        if (response.Errors is { Count: > 0 })
        {
            body["errors"] = response.Errors.Select(error => error.Code is null
                ? (object)new { field = error.Field, message = error.Message }
                : new { field = error.Field, code = error.Code, message = error.Message }).ToArray();
        }

        await context.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }
}
