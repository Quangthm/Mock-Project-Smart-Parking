namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationFacilityOwner
{
    Task<FacilityBookingFacts> ResolveSlotAsync(Guid slotId, CancellationToken ct);
    Task<FacilityBookingFacts> ReadAsync(Guid siteId, Guid slotId, CancellationToken ct);
}
