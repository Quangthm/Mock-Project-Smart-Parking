namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

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

public record PaymentCancellationResult(
    bool IsSuccess,
    string PaymentStatus,
    string? GatewayTransactionId,
    string? ErrorCode,
    string? ErrorMessage
);
