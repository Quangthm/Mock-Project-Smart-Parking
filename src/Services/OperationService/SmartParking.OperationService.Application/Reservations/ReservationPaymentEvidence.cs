namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationPaymentEvidence(Guid EventId, Guid PaymentOrderId, Guid PaymentTransactionId,
    Guid ReservationId, Guid AccountId, Guid TenantId, Guid SiteId, decimal Amount, string Currency,
    string PaymentMethod, string? PlateNumber, DateTimeOffset OccurredAt);
