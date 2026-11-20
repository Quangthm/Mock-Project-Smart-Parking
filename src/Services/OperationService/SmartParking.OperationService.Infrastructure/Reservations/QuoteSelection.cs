namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record QuoteSelection(Guid VehicleId, Guid SiteId, Guid RequestedSlotId,
    DateTimeOffset Start, DateTimeOffset End, string PaymentMethod);
