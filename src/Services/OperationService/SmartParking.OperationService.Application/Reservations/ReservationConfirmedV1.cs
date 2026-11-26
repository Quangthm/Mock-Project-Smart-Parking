using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationConfirmedV1(Guid EventId, Guid ReservationId, Guid AccountId,
    Guid TenantId, Guid SiteId, DateTimeOffset ConfirmedAt);
