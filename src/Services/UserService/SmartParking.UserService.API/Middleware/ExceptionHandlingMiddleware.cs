using FluentValidation;
using System.Net;
using System.Text.Json;
using UserService.Application.Usecase.Login;

namespace UserService.WebAPI.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(
                ex,
                "Validation error for {Path}",
                context.Request.Path);

            await WriteErrorAsync(
                context,
                HttpStatusCode.BadRequest,
                "Invalid input",
                "INVALID_INPUT",
                ex.Errors
                    .Select(x => x.ErrorMessage)
                    .ToArray());
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Authentication failed for {Path}",
                context.Request.Path);

            await WriteErrorAsync(
                context,
                HttpStatusCode.Unauthorized,
                ex.Message,
                "AUTH_FAILED",
                []);
        }
        catch (AccountLockedException ex)
        {
            _logger.LogWarning(
                ex,
                "Account locked for {Path}",
                context.Request.Path);

            await WriteErrorAsync(
                context,
                HttpStatusCode.Forbidden,
                ex.Message,
                "ACCOUNT_LOCKED",
                []);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception for {Path}",
                context.Request.Path);

            await WriteErrorAsync(
                context,
                HttpStatusCode.InternalServerError,
                "Internal server error",
                "INTERNAL_SERVER_ERROR",
                []);
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message,
        string errorCode,
        string[] errors)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            success = false,
            message,
            errorCode,
            errors,
            data = (object?)null
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}