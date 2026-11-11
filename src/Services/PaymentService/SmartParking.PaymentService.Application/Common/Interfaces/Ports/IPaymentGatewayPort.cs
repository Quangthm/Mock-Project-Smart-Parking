namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

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

    Task<PaymentCancellationResult> CancelPaymentAsync(
        long orderCode,
        Guid transactionId,
        string? gatewayTransactionId,
        CancellationToken cancellationToken = default);
}
