namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationFacilityOwner
{
    Task<SelectedSlotMetadata> ResolveSlotAsync(Guid slotId, CancellationToken ct);
}
