namespace SmartParking.OperationService.Domain.PassPlans;

public sealed class PassPlanException(
    int statusCode,
    string code,
    string message,
    string? field = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string? Field { get; } = field;
}
