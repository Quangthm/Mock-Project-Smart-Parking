using SmartParking.ReservationService.Domain.Entities;

namespace SmartParking.ReservationService.Application.Common.Interfaces.Persistence;

public interface ISlotAllocationRepository : IRepository<SlotAllocationRow>
{
    Task<SlotAllocationRow?> FindReservedAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken);
}
