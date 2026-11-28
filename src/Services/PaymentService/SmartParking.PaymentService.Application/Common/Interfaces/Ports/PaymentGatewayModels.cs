namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

/// <summary>
/// Non-verifying routing facts extracted from a provider webhook payload. OrderCode is 0
/// when the payload is unparseable or does not carry an order code.
/// </summary>
public record PaymentWebhookEnvelope(long OrderCode);

public record PaymentInitiateRequest(
    Guid TransactionId,
    long OrderCode,
    long AmountVnd,
    string Description,
    string ReturnUrl,
    string CancelUrl,
    DateTimeOffset ExpiresAt,
    Guid? PaymentOrderId = null
);

public record PaymentInitiateResult(
    bool IsSuccess,
    string? CheckoutUrl,
    string? QrCode,
    string? AccountNumber,
    string? AccountName,
    string? Bin,
    string? GatewayTransactionId,
    string? ErrorCode,
    string? ErrorMessage
);

public record PaymentCancellationResult(
    bool IsSuccess,
    string PaymentStatus,
    string? GatewayTransactionId,
    string? ErrorCode,
    string? ErrorMessage
);
