namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public interface IPaymentSucceededHandler
{
    Task HandleAsync(PaymentSucceededMessage message, CancellationToken ct);
}

public sealed record PaymentSucceededMessage(
    Guid PaymentOrderId,
    Guid PaymentTransactionId,
    string PaymentMethod,
    string? GatewayReference,
    Guid TenantId,
    Guid SiteId,
    string ReferenceType,
    Guid ReferenceId,
    string? PlateNumber,
    Guid? AccountId,
    decimal Amount,
    decimal ParkingFee,
    decimal? PenaltyFee,
    string? PenaltyReason,
    string Currency,
    DateTimeOffset OccurredAt);
