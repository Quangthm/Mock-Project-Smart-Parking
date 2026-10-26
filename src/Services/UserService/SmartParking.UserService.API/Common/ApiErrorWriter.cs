using Microsoft.AspNetCore.Mvc;

namespace UserService.API.Common;

/// <summary>Writes the existing envelope without replacing a response already in flight.</summary>
public sealed class ApiErrorWriter
{
    public async ValueTask<bool> TryWriteAsync(HttpContext context, ApiResponse response,
        CancellationToken cancellationToken = default)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
            return false;

        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentLength = null;
        if (response.StatusCode == StatusCodes.Status401Unauthorized)
            context.Response.Headers.TryAdd("WWW-Authenticate", "Bearer");

        if (HttpMethods.IsHead(context.Request.Method) || response.StatusCode is 204 or 304)
            return true;

        await context.Response.WriteAsJsonAsync(response.Body, cancellationToken);
        return true;
    }
}

/// <summary>Uses the same writer for automatic MVC binding failures.</summary>
public sealed class ApiErrorActionResult(ApiResponse response) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var writer = context.HttpContext.RequestServices.GetRequiredService<ApiErrorWriter>();
        if (!await writer.TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted))
            context.HttpContext.RequestAborted.ThrowIfCancellationRequested();
    }
}
