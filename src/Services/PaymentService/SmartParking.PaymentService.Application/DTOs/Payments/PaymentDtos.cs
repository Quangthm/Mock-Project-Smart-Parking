using System.Text.Json;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Application.DTOs.Payments;

public record PaymentCheckoutRequestDto(
    PaymentMethod PaymentMethod = PaymentMethod.PAYOS,
    string? ReturnUrl = null,
    string? CancelUrl = null
);

public record CreatePaymentOrderRequestDto(
    Guid TenantId,
    Guid SiteId,
    Guid ReferenceId,
    ReferenceType ReferenceType,
    decimal Amount,
    Guid? AccountId = null,
    string? IdempotencyKey = null
);

public record CashPaymentRequestDto(string? IdempotencyKey = null);

public record PaymentCheckoutResponseDto(
    Guid PaymentTransactionId,
    long OrderCode,
    long Amount,
    string CheckoutUrl,
    string QrCode,
    string AccountNumber,
    string AccountName,
    string Bin,
    string Status,
    DateTimeOffset ExpiresAt,
    bool IsReplayed = false
);

public record PayOsWebhookPayloadDto(
    string Code,
    string Desc,
    JsonElement Data,
    string Signature
);

public record PaymentTransactionStatusDto(
    Guid TransactionId,
    Guid OrderId,
    long OrderCode,
    long Amount,
    string PaymentMethod,
    string? GatewayTransactionId,
    string Status,
    DateTimeOffset? PaidAt,
    bool IsAuthoritative
);
