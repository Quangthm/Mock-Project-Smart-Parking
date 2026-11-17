namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public interface IPaymentGatewayPort
{
    string ProviderCode { get; }

    Task<PaymentInitiateResult> InitiatePaymentAsync(
        PaymentInitiateRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts routing facts (for example the order code) from a webhook payload WITHOUT
    /// verifying its signature. Used to resolve the order before order-bound key selection.
    /// </summary>
    PaymentWebhookEnvelope ParseWebhookEnvelope(string rawPayload);

    /// <summary>
    /// Verifies the callback against the signing configuration bound at checkout issuance.
    /// Store-backed gateways reject unbound orders. The optional timestamp is retained for existing callers.
    /// </summary>
    Task<PaymentCallbackResult> VerifyAndParseWebhookAsync(
        string rawPayload, 
        string signature, 
        DateTimeOffset? orderCreatedAt = null,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> QueryTransactionAsync(
        long orderCode, 
        string? gatewayTransactionId, 
        CancellationToken cancellationToken = default);

    Task<PaymentCancellationResult> CancelPaymentAsync(
        long orderCode,
        Guid transactionId,
        string? gatewayTransactionId,
        CancellationToken cancellationToken = default);
}
