using FluentValidation;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.API.Middleware;

public sealed class AuthExceptionMiddleware(RequestDelegate next, ILogger<AuthExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (ValidationException exception)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false, message = "Validation failed.",
                errors = exception.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage })
            });
        }
        catch (AuthException exception)
        {
            if (exception.InnerException is not null)
                logger.LogError(exception.InnerException, "Authentication operation failed: {Code}", exception.Code);
            context.Response.StatusCode = exception.StatusCode;
            await context.Response.WriteAsJsonAsync(new { success = false, code = exception.Code, message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { success = false, code = "INVALID_TOKEN", message = exception.Message });
        }
    }
}
