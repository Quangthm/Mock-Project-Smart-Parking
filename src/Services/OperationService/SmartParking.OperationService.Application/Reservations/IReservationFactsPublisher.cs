using SmartParking.Contracts.ReservationPayments;
namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationFactsPublisher
{
    Task EnsureOrderAsync(EnsureReservationOrderV1 command, CancellationToken ct);
    Task RequestDispositionAsync(RequestReservationDispositionV1 command, CancellationToken ct);
    Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct);
    Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct);
    Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct);
    Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct);
}
