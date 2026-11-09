using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ParkingSessionRepository(ReservationDbContext db)
    : GenericRepository<ParkingSessionRow>(db), IParkingSessionRepository
{
    public async Task<ParkingSessionRow?> FindForUpdateAsync(
        Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken)
    {
        var row = await Set.SingleOrDefaultAsync(
            session => session.Id == sessionId &&
                session.TenantId == tenantId &&
                session.SiteId == siteId,
            cancellationToken);
        if (row is not null) await db.Entry(row).ReloadAsync(cancellationToken);
        return row;
    }
    public async Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsAsync(
        Guid tenantId, Guid siteId, CancellationToken ct)
    {
        var sessions = await db.ParkingSessions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.SiteId == siteId)
            .OrderByDescending(s => s.EntryTime).ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        var ids = sessions.Select(s => s.Id).ToArray();
        var allocations = await db.SlotAllocations.AsNoTracking()
            .Where(a => a.ParkingSessionId != null && ids.Contains(a.ParkingSessionId.Value))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return sessions.Select(s =>
        {
            var slotId = s.CurrentSlotId ?? allocations.FirstOrDefault(a => a.ParkingSessionId == s.Id)?.SlotId;
            return new ParkingSessionHistoryEntry(
                s.Id, s.ReservationId, s.TenantId, s.SiteId, s.AccessItemId, s.AccountId,
                slotId, null, s.OriginalPlate, s.NormalizedPlate, s.VehicleType, s.TicketReference,
                s.EntryTime, s.EntryImageUrl, s.ExitTime, s.ExitImageUrl, s.SessionStatus, s.Version, s.CreatedAt);
        }).ToArray();
    }

    public async Task<WalkInParkingSession?> FindActiveWalkInAsync(
        Guid tenantId, Guid siteId, string plate, CancellationToken ct)
    {
        var normalizedPlate = GateRules.NormalizePlate(plate);
        var session = await db.ParkingSessions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.SiteId == siteId && s.ReservationId == null &&
                s.SessionStatus == "ACTIVE" && s.NormalizedPlate == normalizedPlate)
            .OrderByDescending(s => s.EntryTime)
            .FirstOrDefaultAsync(ct);
        if (session is null)
            return null;
        var allocation = await db.SlotAllocations.AsNoTracking()
            .Where(a => a.ParkingSessionId == session.Id && a.AllocationStatus == "OCCUPIED")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var slot = session.CurrentSlotId ?? allocation?.SlotId;
        return new(session.Id, session.SiteId, session.CurrentSlotId, slot?.ToString(),
            session.OriginalPlate ?? session.NormalizedPlate ?? "", session.VehicleType,
            session.EntryTime, session.ExitTime, session.EntryImageUrl, session.ExitImageUrl);
    }

    public Task<bool> HasActivePlateAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct) =>
        Set.AnyAsync(s => s.TenantId == tenantId && s.SiteId == siteId && s.NormalizedPlate == plate && s.ExitTime == null, ct);
    public async Task<ParkingSessionRow?> FindByReservationAsync(Guid tenantId, Guid siteId, Guid reservationId, bool active, CancellationToken ct)
    {
        var row = await Set.Where(s => s.TenantId == tenantId && s.SiteId == siteId && s.ReservationId == reservationId &&
            (active ? s.ExitTime == null : s.ExitTime != null)).OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
}
