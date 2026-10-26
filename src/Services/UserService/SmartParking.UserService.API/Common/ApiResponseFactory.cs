using System.Collections.Frozen;
using Microsoft.AspNetCore.Mvc;

namespace UserService.API.Common;

/// <summary>
/// The single JSON envelope used by every UserService response.
/// </summary>
/// <remarks>
/// Success bodies carry <c>success</c> and <c>data</c>; failures carry <c>success</c>,
/// <c>code</c> and <c>message</c>, plus <c>errors</c> for validation failures.
/// The shape is the contract the frontend already consumes, so it must not drift.
/// </remarks>
public static class ApiResponseFactory
{
    public const string ValidationFailedCode = "VALIDATION_FAILED";
    public const string InvalidTokenCode = "INVALID_TOKEN";
    public const string ForbiddenCode = "FORBIDDEN";
    public const string InternalErrorCode = "INTERNAL_ERROR";

    // The same approved translations serve exception and non-exception producers.
    // Status remains transport metadata, never a field in the public JSON envelope.
    private static readonly FrozenDictionary<int, ApiResponse> StatusErrors =
        new Dictionary<int, ApiResponse>
        {
            [400] = ValidationError([new ApiErrorDetail("", "The request could not be read.")]),
            [401] = Error(InvalidTokenCode, "Token is invalid, expired or revoked.", 401),
            [403] = Error(ForbiddenCode, "You do not have permission to perform this action.", 403),
            [404] = Error("NOT_FOUND", "The requested resource was not found.", 404),
            [405] = Error("METHOD_NOT_ALLOWED", "The request method is not allowed for this resource.", 405),
            [415] = Error("UNSUPPORTED_MEDIA_TYPE", "The request content type is not supported.", 415),
            [429] = Error("TOO_MANY_REQUESTS", "Too many requests. Please try again later.", 429)
        }.ToFrozenDictionary();

    public static bool TryForStatus(int statusCode, out ApiResponse response) =>
        StatusErrors.TryGetValue(statusCode, out response);

    /// <summary>Successful response carrying the payload.</summary>
    public static ApiResponse Success<T>(T data) =>
        new(StatusCodes.Status200OK, new { success = true, data });

    /// <summary>Successful response carrying a message and the payload.</summary>
    public static ApiResponse Success<T>(T data, string message) =>
        new(StatusCodes.Status200OK, new { success = true, message, data });

    /// <summary>Failed response with a stable machine-readable code.</summary>
    public static ApiResponse Error(string code, string message, int statusCode) =>
        new(statusCode, new { success = false, code, message });

    /// <summary>Failed response describing every rejected field.</summary>
    public static ApiResponse ValidationError(IEnumerable<ApiErrorDetail> errors) =>
        new(StatusCodes.Status400BadRequest, new
        {
            success = false,
            code = ValidationFailedCode,
            message = "Validation failed.",
            errors = errors.Select(error => new { field = error.Field, message = error.Message }).ToArray()
        });
}

/// <summary>A status code and the body to serialize for it.</summary>
public readonly record struct ApiResponse(int StatusCode, object Body)
{
    /// <summary>Materializes the response for a controller action.</summary>
    public IActionResult ToActionResult() => new ObjectResult(Body) { StatusCode = StatusCode };
}

/// <summary>One rejected field and the reason it was rejected.</summary>
public readonly record struct ApiErrorDetail(string Field, string Message);
