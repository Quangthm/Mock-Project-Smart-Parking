using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed partial class ReservationRepository(ReservationDbContext db)
    : GenericRepository<ReservationRow>(db), Application.Reservations.IReservationBookingRepository
{
    public async Task<ReservationRow?> FindForUpdateAsync(
        Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken)
    {
        var row = await Set.SingleOrDefaultAsync(
            reservation => reservation.Id == reservationId &&
                reservation.TenantId == tenantId &&
                reservation.SiteId == siteId,
            cancellationToken);
        if (row is not null) await db.Entry(row).ReloadAsync(cancellationToken);
        return row;
    }
    public async Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        var reservations = await db.Reservations.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.SiteId == siteId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return await ToEntriesAsync(reservations, ct);
    }

    public async Task<ReservationGateEntry?> FindAsync(Guid tenantId, Guid siteId, string query, CancellationToken ct)
    {
        var plate = GateRules.NormalizePlate(query);
        var id = Guid.TryParse(query, out var parsedId) ? parsedId : Guid.Empty;
        var reservation = await db.Reservations.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.SiteId == siteId &&
                (r.Id == id || r.NormalizedPlate == plate))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (reservation is null)
            return null;
        return (await ToEntriesAsync([reservation], ct)).Single();
    }

    public async Task<ReservationGateEntry?> FindByIdAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken ct)
    {
        var reservation = await db.Reservations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == reservationId && r.TenantId == tenantId && r.SiteId == siteId, ct);
        if (reservation is null)
            return null;
        return (await ToEntriesAsync([reservation], ct)).Single();
    }

    private async Task<IReadOnlyList<ReservationGateEntry>> ToEntriesAsync(
        IReadOnlyList<ReservationRow> reservations, CancellationToken ct)
    {
        if (reservations.Count == 0)
            return [];
        var ids = reservations.Select(r => r.Id).ToArray();
        var sessions = await db.ParkingSessions.AsNoTracking()
            .Where(s => s.ReservationId != null && ids.Contains(s.ReservationId.Value))
            .OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
        var allocations = await db.SlotAllocations.AsNoTracking()
            .Where(a => a.ReservationId != null && ids.Contains(a.ReservationId.Value) &&
                new[] { "RESERVED", "OCCUPIED" }.Contains(a.AllocationStatus))
            .OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        return reservations.Select(r =>
        {
            var session = sessions.FirstOrDefault(s => s.ReservationId == r.Id);
            var allocation = allocations.FirstOrDefault(a => a.ReservationId == r.Id);
            var slotId = session?.CurrentSlotId ?? allocation?.SlotId;
            return new ReservationGateEntry(r.Id, r.AccountId, r.SiteId, r.SiteId.ToString(),
                slotId, slotId?.ToString() ?? "Unassigned", r.OriginalPlate ?? r.NormalizedPlate ?? "",
                MapStatus(r.Status), null, null, null, r.ExpectedStartTime, r.ExpectedEndTime,
                session?.EntryTime, session?.ExitTime, session?.EntryImageUrl, session?.ExitImageUrl, r.CreatedAt);
        }).ToArray();
    }

    private static string MapStatus(string status) => status switch
    {
        "CONFIRMED" or "ALLOCATED" => "confirmed",
        "PARKING" => "checked-in",
        "COMPLETED" => "completed",
        "PENDING_PAYMENT" => "pending",
        "CANCELLED" or "EXPIRED" or "NO_SHOW" or "UNFULFILLABLE" => "cancelled",
        _ => status.ToLowerInvariant()
    };

}
