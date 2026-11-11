namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public interface ICheckoutPaymentClient
{
    Task<CheckoutPricing> CalculateAsync(
        Guid tenantId,
        Guid siteId,
        Guid referenceId,
        string referenceType,
        string vehicleType,
        DateTimeOffset entryTime,
        DateTimeOffset pricingCutoffAt,
        CancellationToken ct);

    Task<CheckoutPaymentPreparation> PrepareAsync(
        Guid tenantId,
        Guid siteId,
        Guid referenceId,
        string referenceType,
        string vehicleType,
        DateTimeOffset entryTime,
        DateTimeOffset pricingCutoffAt,
        string plateNumber,
        Guid? accountId,
        CancellationToken ct,
        decimal? developmentTestAmount = null);

    Task<CheckoutPaymentCheckout> InitiateAsync(Guid orderId, string idempotencyKey, CancellationToken ct);
    Task<CheckoutPaymentStatus> ConfirmCashAsync(Guid orderId, string idempotencyKey, CancellationToken ct);
    Task<CheckoutPaymentStatus> DemoPayAsync(Guid orderId, CancellationToken ct);
    Task<CheckoutPaymentStatus> GetStatusAsync(Guid transactionId, CancellationToken ct);
}

public sealed record CheckoutPaymentPreparation(
    CheckoutPricing Pricing,
    CheckoutPaymentOrder Order);

public sealed record CheckoutPricing(
    long FinalAmountVnd,
    string Currency,
    decimal TotalParkedMinutes,
    bool IsFreeStay);

public sealed record CheckoutPaymentOrder(
    Guid OrderId,
    string OrderCode,
    decimal Amount,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record CheckoutPaymentCheckout(
    Guid PaymentTransactionId,
    long OrderCode,
    long Amount,
    string CheckoutUrl,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record CheckoutPaymentStatus(
    Guid TransactionId,
    Guid OrderId,
    long Amount,
    string Status,
    DateTimeOffset? PaidAt,
    bool IsAuthoritative);
