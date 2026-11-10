using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Persistence;

public sealed class VehicleLocationReadStore(ReservationDbContext db) : IVehicleLocationReadStore
{
    public async Task<ActiveVehicleLocation?> FindActiveForAccountAsync(Guid accountId, CancellationToken ct)
    {
        var session = await db.ParkingSessions.AsNoTracking()
            .Where(s => s.AccountId == accountId && s.ExitTime == null && s.SessionStatus == "ACTIVE")
            .OrderByDescending(s => s.EntryTime).ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (session is null)
            return null;

        var allocationStatus = await db.SlotAllocations.AsNoTracking()
            .Where(a => a.ParkingSessionId == session.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.AllocationStatus)
            .FirstOrDefaultAsync(ct);

        // SiteCode falls back to SiteId.ToString() and slot/zone/floor codes are null because
        // OperationsDb has no local code tables; Facility-backed lookup is deferred to Phase 2.
        return new ActiveVehicleLocation(
            session.Id,
            session.SiteId,
            session.SiteId.ToString(),
            session.CurrentSlotId,
            session.CurrentSpatialUnitId,
            allocationStatus,
            null,
            null,
            null,
            null,
            session.EntryTime);
    }
}