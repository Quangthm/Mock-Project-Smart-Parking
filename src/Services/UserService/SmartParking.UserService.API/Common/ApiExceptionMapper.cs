using System.Collections.Frozen;
using FluentValidation;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.API.Common;

/// <summary>A public response plus the diagnostic classification; neither leaks into the JSON body.</summary>
public readonly record struct ErrorHandlingResult(ApiResponse Response, bool IsUnexpected = false);

/// <summary>
/// Immutable, exact-runtime-type dispatch. Subclasses need an explicit registration;
/// an unregistered type (including database errors) receives the safe 500 fallback.
/// </summary>
public sealed class ApiExceptionMapper
{
    private readonly FrozenDictionary<Type, Func<Exception, ErrorHandlingResult>> mappings;

    public ApiExceptionMapper(IEnumerable<KeyValuePair<Type, Func<Exception, ErrorHandlingResult>>> registrations)
    {
        var dictionary = new Dictionary<Type, Func<Exception, ErrorHandlingResult>>();
        foreach (var registration in registrations)
        {
            if (!typeof(Exception).IsAssignableFrom(registration.Key))
                throw new ArgumentException("Only exception types can be registered.", nameof(registrations));
            ArgumentNullException.ThrowIfNull(registration.Value);
            if (!dictionary.TryAdd(registration.Key, registration.Value))
                throw new ArgumentException($"Duplicate exception mapping: {registration.Key.Name}.", nameof(registrations));
        }
        mappings = dictionary.ToFrozenDictionary();
    }

    public ErrorHandlingResult Map(Exception exception) =>
        mappings.TryGetValue(exception.GetType(), out var mapping) ? mapping(exception) : Unexpected();

    public static ErrorHandlingResult Unexpected() => new(ApiResponseFactory.Error(
        ApiResponseFactory.InternalErrorCode, "An unexpected error occurred.", StatusCodes.Status500InternalServerError), true);

    public static ApiExceptionMapper CreateDefault()
    {
        // AuthException is a legacy application rejection type. Only known codes
        // have a public translation; its arbitrary Message/StatusCode are not trusted.
        var authenticationErrors = CreateAuthenticationErrors();
        return new(new Dictionary<Type, Func<Exception, ErrorHandlingResult>>
        {
            [typeof(ValidationException)] = exception => new(ApiResponseFactory.ValidationError(
                ((ValidationException)exception).Errors.Select(error => new ApiErrorDetail(error.PropertyName, error.ErrorMessage)))),
            [typeof(AuthException)] = exception => authenticationErrors.TryGetValue(
                (((AuthException)exception).Code, ((AuthException)exception).StatusCode), out var result)
                ? result : Unexpected(),
            [typeof(BadHttpRequestException)] = exception =>
                ApiResponseFactory.TryForStatus(((BadHttpRequestException)exception).StatusCode, out var response)
                    ? new(response) : Unexpected(),
            [typeof(ProfileAccessConflictException)] = _ => new(ApiResponseFactory.Error(
                "PROFILE_ACCESS_CONFLICT", "Profile access changed or is busy. Retry the request.",
                StatusCodes.Status409Conflict))
        });
    }

    private static FrozenDictionary<(string Code, int Status), ErrorHandlingResult> CreateAuthenticationErrors()
    {
        var errors = new Dictionary<(string Code, int Status), ErrorHandlingResult>();
        foreach (var code in new[] { "AUTH_FAILED", "ACCOUNT_PENDING_APPROVAL", "ACCOUNT_PENDING_VERIFICATION",
                     "ACCOUNT_REJECTED", "ACCOUNT_INACTIVE", "ACCOUNT_ACCESS_DENIED", "ACCOUNT_LOCKED", "MFA_REQUIRED" })
        {
            var failure = SignInErrors.ForCode(code);
            errors.Add((code, failure.StatusCode), new(ApiResponseFactory.Error(code, failure.Message, failure.StatusCode)));
        }
        ApiResponseFactory.TryForStatus(StatusCodes.Status401Unauthorized, out var invalidToken);
        ApiResponseFactory.TryForStatus(StatusCodes.Status403Forbidden, out var forbidden);
        errors.Add((ApiResponseFactory.InvalidTokenCode, 401), new(invalidToken));
        errors.Add((ApiResponseFactory.ForbiddenCode, 403), new(forbidden));
        Add("MISSING_REFRESH_TOKEN", "Refresh token is required.", 400);
        Add("REVOKE_SESSION_FAILED", "Could not revoke the session. Please try again.", 500);
        Add("INVALID_PLATE", "Plate does not match the supported Vietnamese application format.", 400);
        Add("VALIDATION_FAILED", "Validation failed.", 400);
        Add("VEHICLE_NOT_FOUND", "Vehicle was not found.", 404);
        Add("PROFILE_NOT_FOUND", "Profile was not found.", 404);
        Add("PLATE_EXISTS", "A vehicle with this plate is already registered.", 409);
        // Preserve existing workflow outcomes while exposing only reviewed fixed text.
        // CONTACT_UNVERIFIED has two existing contexts: OTP sign-in and Owner approval.
        Add("CONTACT_UNVERIFIED", "Use a verified contact for OTP sign in.", 401);
        Add("CONTACT_UNVERIFIED", "Verify required contact before approval.", 409);
        Add("CONTACT_EXISTS", "Email or phone is already registered.", 409);
        Add("EMAIL_EXISTS", "Email is already registered.", 409);
        Add("REGISTRATION_NOT_FOUND", "Registration was not found.", 404);
        Add("REGISTRATION_CLOSED", "Registration is no longer pending.", 409);
        Add("CHALLENGE_CLOSED", "Challenge is not available for this purpose.", 409);
        Add("OTP_LOCKED", "Too many incorrect codes. Try again after the lock expires.", 423);
        Add("OTP_COOLDOWN", "Wait before requesting another code.", 429);
        Add("OTP_EXPIRED", "Code has expired. Request a new code.", 400);
        Add("OTP_INVALID", "Incorrect verification code.", 400);
        Add("MFA_ALREADY_ENABLED", "MFA is already enabled.", 409);
        Add("MFA_INVALID", "Invalid authenticator code.", 400);
        Add("DELIVERY_NOT_FOUND", "Delivery was not found.", 404);
        Add("APPLICATION_NOT_FOUND", "Owner application was not found.", 404);
        Add("APPLICATION_CLOSED", "Owner application is no longer pending.", 409);
        Add("SITE_ACCESS_DENIED", "Every assigned site must be active and owned by this Owner.", 403);
        Add("USER_NOT_FOUND", "User was not found.", 404);
        Add("ADMIN_ACCOUNT_PROTECTED", "Admin accounts cannot be changed through user management.", 409);
        Add("INVALID_STATUS_TRANSITION", "Verification or owner approval must complete before managing access.", 409);
        Add("PARKING_UNAVAILABLE", "Parking service is unavailable. Please retry.", 503);
        return errors.ToFrozenDictionary();

        void Add(string code, string message, int status) =>
            errors.Add((code, status), new(ApiResponseFactory.Error(code, message, status), status >= 500));
    }
}
