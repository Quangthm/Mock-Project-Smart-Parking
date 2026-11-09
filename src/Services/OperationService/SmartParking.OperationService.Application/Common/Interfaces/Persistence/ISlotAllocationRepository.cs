using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Common.Interfaces.Persistence;

public interface ISlotAllocationRepository : IRepository<SlotAllocationRow>
{
    Task<SlotAllocationRow?> FindReservedAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SlotAllocationRow>> FindOccupiedAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken ct);
}
