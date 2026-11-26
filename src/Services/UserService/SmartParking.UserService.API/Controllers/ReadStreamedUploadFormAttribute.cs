using Microsoft.AspNetCore.Mvc.Filters;
using UserService.API.Common;

namespace UserService.API.Controllers;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ReadStreamedUploadFormAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength is null && request.HasFormContentType)
        {
            // MVC wraps form IO exceptions as generic binding errors. Read the cached
            // form after the native limit filters so a streamed 413 reaches the mapper.
            try
            {
                await request.ReadFormAsync(context.HttpContext.RequestAborted);
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException and not BadHttpRequestException)
            {
                ApiResponseFactory.TryForStatus(StatusCodes.Status400BadRequest, out var response);
                context.Result = response.ToActionResult();
                return;
            }
        }
        await next();
    }
}
