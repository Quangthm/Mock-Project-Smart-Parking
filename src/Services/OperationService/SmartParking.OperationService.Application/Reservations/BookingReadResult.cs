using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record BookingReadResult(
    ReservationRow Reservation, Guid? AllocatedSlotId, string? AllocationStatus, Guid? ParkingSessionId);
