using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface ISlotAllocationRepository : IRepository<SlotAllocationRow>
{
    Task<IReadOnlyList<SlotAllocationRow>> FindActiveForVisitAsync(
        Guid tenantId, Guid siteId, Guid sessionId, Guid? reservationId, CancellationToken ct);
}
