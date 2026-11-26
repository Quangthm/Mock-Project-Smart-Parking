using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationAvailabilityFacts(IReadOnlyList<ReservationRow> Reservations,
    IReadOnlyList<ParkingSessionRow> Sessions, IReadOnlyList<SlotAllocationRow> Allocations,
    int? MaxReservationQuota);
