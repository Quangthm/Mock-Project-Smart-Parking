namespace SmartParking.PaymentService.Domain.Ports;

public interface IPaymentGatewayPort
{
    string ProviderCode { get; }

    Task<PaymentInitiateResult> InitiatePaymentAsync(
        PaymentInitiateRequest request, 
        CancellationToken cancellationToken = default);

    Task<PaymentCallbackResult> VerifyAndParseWebhookAsync(
        string rawPayload, 
        string signature, 
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> QueryTransactionAsync(
        long orderCode, 
        string? gatewayTransactionId, 
        CancellationToken cancellationToken = default);
}
