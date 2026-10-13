using FluentValidation;
using System.Net;
using UserService.Application.Usecase.Login;

namespace UserService.WebAPI.Middleware;

public static class ErrorExceptionHandler
{
    public static (HttpStatusCode StatusCode, object Response)
        Handle(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException =>
            (
                HttpStatusCode.BadRequest,
                new
                {
                    success = false,
                    message = "Invalid input.",
                    errorCode = "INVALID_INPUT",
                    errors = validationException.Errors
                        .Select(x => x.ErrorMessage)
                        .ToArray(),
                    data = (object?)null
                }
            ),

            UnauthorizedAccessException unauthorizedException =>
            (
                HttpStatusCode.Unauthorized,
                new
                {
                    success = false,
                    message = unauthorizedException.Message,
                    errorCode = "AUTH_FAILED",
                    errors = Array.Empty<string>(),
                    data = (object?)null
                }
            ),

            AccountLockedException lockedException =>
            (
                HttpStatusCode.Forbidden,
                new
                {
                    success = false,
                    message = lockedException.Message,
                    errorCode = "ACCOUNT_LOCKED",
                    errors = Array.Empty<string>(),
                    data = (object?)null
                }
            ),

            _ =>
            (
                HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "Internal server error.",
                    errorCode = "INTERNAL_SERVER_ERROR",
                    errors = Array.Empty<string>(),
                    data = (object?)null
                }
            )
        };
    }
}