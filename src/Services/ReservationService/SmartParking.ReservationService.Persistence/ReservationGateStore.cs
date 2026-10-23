using Microsoft.EntityFrameworkCore;
using SmartParking.ReservationService.Application.Common.Interfaces.Persistence;
using SmartParking.ReservationService.Application.Common.Models.Exceptions;
using SmartParking.ReservationService.Application.DTOs;

namespace SmartParking.ReservationService.Persistence;

public sealed class ReservationGateStore(ReservationDbContext db) : IReservationGateStore
{
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
        var plate = NormalizePlate(query);
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

    public async Task<WalkInParkingSession?> FindActiveWalkInAsync(
        Guid tenantId, Guid siteId, string plate, CancellationToken ct)
    {
        var normalizedPlate = NormalizePlate(plate);
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

    public async Task<WalkInParkingSession> CreateWalkInAsync(
        Guid tenantId, Guid siteId, Guid sessionId, string vehicleType, string plate, string? imageData, CancellationToken ct)
    {
        var normalizedPlate = NormalizePlate(plate);
        if (normalizedPlate.Length is < 2 or > 50)
            throw new ReservationGateException(400, "INVALID_PLATE", "Enter a valid vehicle plate.");
        if (vehicleType is not ("CAR" or "MOTORCYCLE"))
            throw new ReservationGateException(400, "INVALID_VEHICLE_TYPE", "Vehicle type must be CAR or MOTORCYCLE.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockPlateAsync(siteId, normalizedPlate, ct);
        if (await db.ParkingSessions.AnyAsync(s => s.TenantId == tenantId && s.SiteId == siteId &&
            s.NormalizedPlate == normalizedPlate && s.ExitTime == null, ct))
            throw new ReservationGateException(409, "VEHICLE_ALREADY_INSIDE", "An active parking session already exists for this plate.");

        var now = DateTimeOffset.UtcNow;
        db.ParkingSessions.Add(new ParkingSessionRow
        {
            Id = sessionId, TenantId = tenantId, SiteId = siteId, OriginalPlate = plate.Trim(),
            NormalizedPlate = normalizedPlate, VehicleType = vehicleType,
            TicketReference = $"WALKIN-{sessionId:N}", EntryTime = now, EntryImageUrl = imageData,
            SessionStatus = "ACTIVE", CreatedAt = now, Version = 1
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(sessionId, siteId, null, null, plate.Trim(), vehicleType, now, null, imageData, null);
    }

    public async Task<WalkInParkingSession> CheckOutWalkInAsync(
        Guid tenantId, Guid siteId, Guid sessionId, string plate, string? imageData, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await db.ParkingSessions
            .FromSqlInterpolated($"SELECT * FROM parking_sessions WHERE id = {sessionId} AND tenant_id = {tenantId} AND site_id = {siteId} AND reservation_id IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (session is null)
            throw new ReservationGateException(404, "WALK_IN_SESSION_NOT_FOUND", "Active walk-in session was not found.");
        var storedPlate = session.OriginalPlate ?? session.NormalizedPlate ?? "";
        if (NormalizePlate(storedPlate) != NormalizePlate(plate))
            throw new ReservationGateException(409, "PLATE_MISMATCH", "The scanned plate does not match this parking session.");
        if (session.VehicleType is not ("CAR" or "MOTORCYCLE"))
            throw new ReservationGateException(409, "INVALID_VEHICLE_TYPE", "This parking session has an unsupported vehicle type.");

        if (session.ExitTime is null)
        {
            var now = DateTimeOffset.UtcNow;
            session.ExitTime = now;
            session.ExitImageUrl = imageData;
            session.SessionStatus = "COMPLETED";
            if (session.CurrentSlotId is not null)
            {
                var allocations = await db.SlotAllocations
                    .Where(a => a.ParkingSessionId == sessionId && a.AllocationStatus == "OCCUPIED")
                    .ToListAsync(ct);
                foreach (var allocation in allocations)
                {
                    allocation.AllocationStatus = "RELEASED";
                    allocation.AllocatedEndTime = now;
                }
            }
            await db.SaveChangesAsync(ct);
        }

        var currentSlot = session.CurrentSlotId;
        await transaction.CommitAsync(ct);
        return new(session.Id, session.SiteId, currentSlot, currentSlot?.ToString(), storedPlate,
            session.VehicleType, session.EntryTime, session.ExitTime, session.EntryImageUrl, session.ExitImageUrl);
    }

    public Task<ReservationGateEntry> CheckInAsync(
        Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct) =>
        TransitionAsync(tenantId, siteId, actorId, reservationId, recognizedPlate, imageData, true, ct);

    public Task<ReservationGateEntry> CheckOutAsync(
        Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct) =>
        TransitionAsync(tenantId, siteId, actorId, reservationId, recognizedPlate, imageData, false, ct);

    private async Task<ReservationGateEntry> TransitionAsync(
        Guid tenantId, Guid siteId, Guid actorId, Guid reservationId,
        string? recognizedPlate, string? imageData, bool checkIn, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var reservation = await db.Reservations
            .FromSqlInterpolated($"SELECT * FROM reservations WHERE id = {reservationId} AND tenant_id = {tenantId} AND site_id = {siteId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (reservation is null)
            throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found at this site.");

        var plate = reservation.OriginalPlate ?? reservation.NormalizedPlate ?? "";
        var expectedStatus = checkIn
            ? reservation.Status is "CONFIRMED" or "ALLOCATED"
            : reservation.Status == "PARKING";
        if (!expectedStatus)
        {
            if (!checkIn && reservation.Status == "COMPLETED")
            {
                VerifyPlate(plate, recognizedPlate);
                if (await db.ParkingSessions.AnyAsync(s => s.ReservationId == reservationId &&
                    s.TenantId == tenantId && s.SiteId == siteId && s.ExitTime != null, ct))
                {
                    await transaction.RollbackAsync(ct);
                    return await FindByIdAsync(tenantId, siteId, reservationId, ct)
                        ?? throw new InvalidOperationException("Reservation disappeared after a completed gate transition.");
                }
            }
            throw new ReservationGateException(409, "INVALID_RESERVATION_STATE",
                checkIn ? "Only confirmed reservations can check in." : "Only checked-in reservations can check out.");
        }
        VerifyPlate(plate, recognizedPlate);

        var now = DateTimeOffset.UtcNow;
        if (checkIn)
        {
            var allocation = await db.SlotAllocations
                .Where(a => a.ReservationId == reservationId && a.TenantId == tenantId &&
                    a.SiteId == siteId && a.AllocationStatus == "RESERVED")
                .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);
            var sessionId = Guid.NewGuid();
            var slotId = allocation?.SlotId ?? reservation.TargetSlotId;
            db.ParkingSessions.Add(new ParkingSessionRow
            {
                Id = sessionId, ReservationId = reservationId, TenantId = tenantId, SiteId = siteId,
                AccountId = reservation.AccountId, CurrentSlotId = slotId, OriginalPlate = plate,
                NormalizedPlate = NormalizePlate(plate), VehicleType = reservation.VehicleType,
                TicketReference = $"GATE-{sessionId:N}", EntryTime = now, EntryImageUrl = imageData,
                SessionStatus = "ACTIVE", Version = 1, CreatedAt = now
            });
            if (allocation is not null)
            {
                allocation.AllocationStatus = "OCCUPIED";
                allocation.ParkingSessionId = sessionId;
            }
            reservation.Status = "PARKING";
        }
        else
        {
            var session = await db.ParkingSessions
                .FromSqlInterpolated($"SELECT * FROM parking_sessions WHERE reservation_id = {reservationId} AND tenant_id = {tenantId} AND site_id = {siteId} AND session_status = 'ACTIVE' FOR UPDATE")
                .FirstOrDefaultAsync(ct);
            if (session is null)
                throw new ReservationGateException(409, "ACTIVE_SESSION_NOT_FOUND", "No active parking session exists for this reservation.");
            session.ExitTime = now;
            session.ExitImageUrl = imageData;
            session.SessionStatus = "COMPLETED";
            reservation.Status = "COMPLETED";
            var allocations = await db.SlotAllocations
                .Where(a => a.ParkingSessionId == session.Id && a.AllocationStatus == "OCCUPIED")
                .ToListAsync(ct);
            foreach (var allocation in allocations)
            {
                allocation.AllocationStatus = "RELEASED";
                allocation.AllocatedEndTime = now;
            }
        }

        db.AuditLogs.Add(new AuditLogRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, ActorId = actorId,
            ActorRole = "operator", Action = checkIn ? "VEHICLE_CHECK_IN" : "VEHICLE_CHECK_OUT",
            TargetEntity = "RESERVATION", TargetId = reservationId,
            Reason = $"Gate action for plate {plate}.", CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await FindByIdAsync(tenantId, siteId, reservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared after a successful gate transition.");
    }

    private async Task<ReservationGateEntry?> FindByIdAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken ct)
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
            var slotId = session?.CurrentSlotId ?? allocation?.SlotId ?? r.TargetSlotId;
            return new ReservationGateEntry(r.Id, r.AccountId, r.SiteId, r.SiteId.ToString(),
                slotId, slotId?.ToString() ?? "Unassigned", r.OriginalPlate ?? r.NormalizedPlate ?? "",
                MapStatus(r.Status), null, null, null, r.ExpectedStartTime, r.ExpectedEndTime,
                session?.EntryTime, session?.ExitTime, session?.EntryImageUrl, session?.ExitImageUrl, r.CreatedAt);
        }).ToArray();
    }

    private Task<int> LockPlateAsync(Guid siteId, string plate, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({$"{siteId:N}:{plate}"}, 0))", ct);

    private static void VerifyPlate(string expected, string? recognized)
    {
        if (!string.IsNullOrWhiteSpace(recognized) && NormalizePlate(expected) != NormalizePlate(recognized))
            throw new ReservationGateException(409, "PLATE_MISMATCH", "The scanned plate does not match this reservation.");
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

    private static string NormalizePlate(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
