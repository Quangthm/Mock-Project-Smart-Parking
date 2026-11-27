namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public record PaymentVerificationResult(
    bool IsFound,
    string PaymentStatus, // "SUCCESS", "FAILED", "PENDING", "UNKNOWN"
    long AmountVnd,
    string? GatewayTransactionId,
    DateTimeOffset? PaidAt,
    IReadOnlyList<ReviewedPayOsTransfer>? ReviewedTransfers = null
);
