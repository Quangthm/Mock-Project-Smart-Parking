using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace SmartParking.PaymentService.API.Common;

public sealed record PaymentErrorResponse(
    int StatusCode,
    string Code,
    string Message,
    IReadOnlyList<PaymentErrorDetail>? Errors = null)
{
    private static readonly FrozenDictionary<int, PaymentErrorResponse> StatusErrors =
        new Dictionary<int, PaymentErrorResponse>
        {
            [StatusCodes.Status400BadRequest] = Error(StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The request is invalid."),
            [StatusCodes.Status401Unauthorized] = Error(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Authentication is required."),
            [StatusCodes.Status403Forbidden] = Error(StatusCodes.Status403Forbidden, "FORBIDDEN", "Access is not permitted."),
            [StatusCodes.Status404NotFound] = Error(StatusCodes.Status404NotFound, "NOT_FOUND", "The requested resource was not found."),
            [StatusCodes.Status405MethodNotAllowed] = Error(StatusCodes.Status405MethodNotAllowed, "METHOD_NOT_ALLOWED", "The HTTP method is not supported."),
            [StatusCodes.Status413PayloadTooLarge] = Error(StatusCodes.Status413PayloadTooLarge, "PAYLOAD_TOO_LARGE", "The request payload exceeds the permitted limit."),
            [StatusCodes.Status415UnsupportedMediaType] = Error(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE", "The request content type is not supported."),
            [StatusCodes.Status429TooManyRequests] = Error(StatusCodes.Status429TooManyRequests, "TOO_MANY_REQUESTS", "Too many requests. Please try again later.")
        }.ToFrozenDictionary();

    public static bool TryForStatus(int statusCode, [NotNullWhen(true)] out PaymentErrorResponse? response) =>
        StatusErrors.TryGetValue(statusCode, out response);

    public static PaymentErrorResponse Error(
        int statusCode,
        string code,
        string message,
        IReadOnlyList<PaymentErrorDetail>? errors = null) =>
        new(statusCode, code, message, errors);

    public static PaymentErrorResponse ValidationError(IEnumerable<PaymentErrorDetail> errors) =>
        new(
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            "Validation failed for the requested operation.",
            errors.ToArray());
}
