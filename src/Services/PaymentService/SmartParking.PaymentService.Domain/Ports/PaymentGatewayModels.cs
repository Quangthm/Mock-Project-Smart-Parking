namespace SmartParking.PaymentService.Domain.Ports;

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
