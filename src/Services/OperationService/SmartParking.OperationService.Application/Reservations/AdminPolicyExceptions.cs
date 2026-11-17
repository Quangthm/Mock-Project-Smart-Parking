namespace SmartParking.OperationService.Application.Reservations;

public sealed class ForbiddenScopeException(string message) : Exception(message);

public sealed class PolicyNotFoundException(string message) : Exception(message);

public sealed class CannotModifyActivePolicyException(string message) : Exception(message);

public sealed class ResourceVersionConflictException(string message) : Exception(message);

public sealed class OverlappingPolicyIntervalException(string message, string? property = null) : Exception(message)
{
    public string? Property { get; } = property;
}

public sealed class CannotActivateNonDraftException(string message) : Exception(message);

public sealed class InvalidPolicyBoundsException : Exception
{
    public InvalidPolicyBoundsException(string message,
        IEnumerable<(string Property, string Message)>? errors = null) : base(message)
    {
        Errors = errors?.ToArray() ?? [];
    }

    public IReadOnlyList<(string Property, string Message)> Errors { get; }
}

public sealed class InvalidRequestException(string message) : Exception(message);

public sealed class SiteTenantMismatchException(string message) : Exception(message);

public sealed class RequestValidationFailedException : Exception
{
    public RequestValidationFailedException(string message,
        IEnumerable<(string Property, string Message)>? errors = null) : base(message)
    {
        Errors = errors?.ToArray() ?? [];
    }

    public IReadOnlyList<(string Property, string Message)> Errors { get; }
}