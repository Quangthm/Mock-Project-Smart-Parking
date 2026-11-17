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
    DateTimeOffset ExpiresAt
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

public record PaymentCallbackResult(
    bool IsValidSignature,
    long OrderCode,
    long AmountVnd,
    string? GatewayTransactionId,
    string? BankReference,
    string PaymentStatus, // "SUCCESS", "FAILED", "PENDING"
    DateTimeOffset TransactionTime,
    string? RawPayload
);

public record PaymentVerificationResult(
    bool IsFound,
    string PaymentStatus, // "SUCCESS", "FAILED", "PENDING", "UNKNOWN"
    long AmountVnd,
    string? GatewayTransactionId,
    DateTimeOffset? PaidAt
);

public record PaymentCancellationResult(
    bool IsSuccess,
    string PaymentStatus,
    string? GatewayTransactionId,
    string? ErrorCode,
    string? ErrorMessage
);
