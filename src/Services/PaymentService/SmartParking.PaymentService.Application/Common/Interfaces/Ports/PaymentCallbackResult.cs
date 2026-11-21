namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public record PaymentCallbackResult(
    bool IsValidSignature,
    long OrderCode,
    long AmountVnd,
    string? GatewayTransactionId,
    string? BankReference,
    string PaymentStatus, // "SUCCESS", "FAILED", "PENDING"
    DateTimeOffset TransactionTime,
    string? RawPayload,
    DateTimeOffset? VerifiedTransferTime = null
);
