using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class SlotAllocationRepository(ReservationDbContext db)
    : GenericRepository<SlotAllocationRow>(db), ISlotAllocationRepository
{
    public async Task<IReadOnlyList<SlotAllocationRow>> FindActiveForVisitAsync(
        Guid tenantId, Guid siteId, Guid sessionId, Guid? reservationId, CancellationToken ct) =>
        await Set.Where(a => a.TenantId == tenantId && a.SiteId == siteId &&
            (a.AllocationStatus == "RESERVED" || a.AllocationStatus == "OCCUPIED") &&
            ((a.ParkingSessionId == sessionId && (a.ReservationId == null || a.ReservationId == reservationId)) ||
             (reservationId != null && a.ReservationId == reservationId && a.ParkingSessionId == null &&
              a.AllocationStatus == "RESERVED"))).ToListAsync(ct);
}
