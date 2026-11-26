using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationPricePreview(Guid SiteId, Guid RequestedSlotId,
    DateTimeOffset Start, DateTimeOffset End, Guid? VehicleId, string? PaymentMethod);
