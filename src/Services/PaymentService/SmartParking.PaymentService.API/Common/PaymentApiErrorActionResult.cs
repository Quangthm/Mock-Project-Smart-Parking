using Microsoft.AspNetCore.Mvc;

namespace SmartParking.PaymentService.API.Common;

public sealed class PaymentApiErrorActionResult(PaymentErrorResponse response) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var writer = context.HttpContext.RequestServices.GetRequiredService<PaymentApiErrorWriter>();
        if (!await writer.TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted))
        {
            context.HttpContext.RequestAborted.ThrowIfCancellationRequested();
        }
    }
}
