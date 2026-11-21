namespace SmartParking.OperationService.Application.Discovery;

public sealed class DiscoveryFailure(int status, string code, int? retryAfter = null) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public int? RetryAfter { get; } = retryAfter;
    public static DiscoveryFailure Invalid() => new(400, "VALIDATION_FAILED");
    public static DiscoveryFailure Unavailable() => new(503, "DEPENDENCY_UNAVAILABLE");
    public static DiscoveryFailure Changed() => new(409, "SEARCH_CHANGED");
}
