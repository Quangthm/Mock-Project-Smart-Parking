using Microsoft.AspNetCore.Mvc;

namespace SmartParking.NotificationAuditService.API.Common;

public sealed class NotificationApiErrorActionResult(NotificationErrorResponse response) : IActionResult
{
    public NotificationErrorResponse Response { get; } = response;

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var writer = context.HttpContext.RequestServices.GetRequiredService<NotificationApiErrorWriter>();
        if (!await writer.TryWriteAsync(context.HttpContext, Response, context.HttpContext.RequestAborted))
        {
            context.HttpContext.RequestAborted.ThrowIfCancellationRequested();
        }
    }
}
