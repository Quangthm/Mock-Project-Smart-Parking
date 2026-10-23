using Microsoft.EntityFrameworkCore;
using SmartParking.ReservationService.Application.Common.Interfaces.Persistence;
using SmartParking.ReservationService.Domain.Entities;

namespace SmartParking.ReservationService.Persistence.Repositories;

public sealed class SlotAllocationRepository(ReservationDbContext db)
    : GenericRepository<SlotAllocationRow>(db), ISlotAllocationRepository
{
    public Task<SlotAllocationRow?> FindReservedAsync(
        Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken) =>
        Set.Where(a => a.TenantId == tenantId && a.SiteId == siteId &&
                       a.ReservationId == reservationId && a.AllocationStatus == "RESERVED")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
