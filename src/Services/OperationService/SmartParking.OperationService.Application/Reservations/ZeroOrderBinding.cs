namespace SmartParking.OperationService.Application.Reservations;

public sealed record ZeroOrderBinding(Guid OperationId, Guid ReservationId, Guid AccountId, Guid TenantId,
    Guid SiteId, Guid SlotId, string Plate, DateTimeOffset Start, DateTimeOffset End, string Method,
    Guid QuoteId, string QuoteVersion, bool IsCovered, string CoverageVersion, string Currency, decimal Amount, DateTimeOffset HoldExpiresAt);
