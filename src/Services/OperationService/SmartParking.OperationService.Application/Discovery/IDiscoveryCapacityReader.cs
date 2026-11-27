using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Discovery;

public interface IDiscoveryCapacityReader
{
    // Supply only complete Facility-authorized candidates for this trusted tenant and site.
    Task<CarCapacityRead> ReadCarsAsync(Guid tenantId, Guid siteId, IReadOnlyCollection<Guid> candidateSlots,
        ReservationInterval? requestedStay, CancellationToken ct);
}
