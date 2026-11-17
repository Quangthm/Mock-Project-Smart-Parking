namespace SmartParking.PaymentService.Application.Integrations;

/// <summary>
/// A single field-level validation error surfaced by the payment integration endpoints.
/// </summary>
public sealed record ApiFieldError(string Property, string Message);

/// <summary>
/// Application exception used by the payment integration handlers to signal a
/// machine-readable error code to the API layer. HTTP status mapping belongs to
/// the API layer and is intentionally not carried here.
/// </summary>
public sealed class PaymentIntegrationException : Exception
{
    public PaymentIntegrationException(
        string code,
        string message,
        IReadOnlyList<ApiFieldError>? errors = null)
        : base(message)
    {
        Code = code;
        Errors = errors;
    }

    public string Code { get; }

    public IReadOnlyList<ApiFieldError>? Errors { get; }
}
