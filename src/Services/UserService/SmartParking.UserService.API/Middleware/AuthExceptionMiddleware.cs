using FluentValidation;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.API.Middleware;

public sealed class AuthExceptionMiddleware(RequestDelegate next, ILogger<AuthExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected. Do not turn cancellation into an internal-error response.
        }
        catch (ValidationException exception) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false, code = "VALIDATION_FAILED", message = "Validation failed.",
                errors = exception.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage })
            });
        }
        catch (AuthException exception) when (!context.Response.HasStarted)
        {
            if (exception.InnerException is not null)
                logger.LogError(exception.InnerException, "Authentication operation failed: {Code}", exception.Code);
            context.Response.StatusCode = exception.StatusCode;
            await context.Response.WriteAsJsonAsync(new { success = false, code = exception.Code, message = exception.Message });
        }
        catch (UnauthorizedAccessException) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { success = false, code = "INVALID_TOKEN", message = "Authentication is required." });
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError(exception, "Unhandled request failure {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { success = false, code = "INTERNAL_ERROR",
                message = "An unexpected error occurred.", traceId = context.TraceIdentifier });
        }
    }
}
