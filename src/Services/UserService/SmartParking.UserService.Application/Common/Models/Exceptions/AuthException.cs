namespace UserService.Application.Common.Models.Exceptions;

public sealed class AuthException(string code, string message, int statusCode, Exception? inner = null)
    : Exception(message, inner)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;

    public static AuthException InvalidToken() => new("INVALID_TOKEN", "Token is invalid, expired or revoked.", 401);
}
