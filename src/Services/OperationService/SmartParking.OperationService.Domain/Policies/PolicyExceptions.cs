namespace SmartParking.OperationService.Domain.Policies;

public class PolicyDomainException(string message) : Exception(message);

public sealed class PolicyOutOfBoundsException(string parameterName, string message)
    : PolicyDomainException(message)
{
    public string ParameterName { get; } = parameterName;
}

public sealed class InvalidPolicyDurationException(string message)
    : PolicyDomainException(message);

public sealed class CrossLotAccessException(string message = "You do not own this parking lot.")
    : PolicyDomainException(message);

public sealed class LotPolicyNotFoundException(string code, string message)
    : PolicyDomainException(message)
{
    public string Code { get; } = code;
}

public sealed class NoDraftToPublishException(string message = "No draft policy exists to publish.")
    : PolicyDomainException(message);

public sealed class IdempotencyPayloadMismatchException(string message = "Idempotency key payload mismatch.")
    : PolicyDomainException(message);

public sealed class PolicyOperationInProgressException(string message = "Another publish operation is in progress.")
    : PolicyDomainException(message);
