using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class SlotAllocationRepository(ReservationDbContext db)
    : GenericRepository<SlotAllocationRow>(db), ISlotAllocationRepository
{
    public Task<SlotAllocationRow?> FindReservedAsync(
        Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken) =>
        Set.Where(a => a.TenantId == tenantId && a.SiteId == siteId &&
                       a.ReservationId == reservationId && a.AllocationStatus == "RESERVED")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<SlotAllocationRow>> FindOccupiedAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken ct) =>
        await Set.Where(a => a.TenantId == tenantId && a.SiteId == siteId && a.ParkingSessionId == sessionId && a.AllocationStatus == "OCCUPIED").ToListAsync(ct);
}
