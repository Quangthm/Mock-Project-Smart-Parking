using MediatR;
using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckInWalkInCommandHandler(IOperatorIdentityClient identity, IParkingSessionRepository sessions, IOperationsAuditPublisher audit,
    IOperationsConcurrency guards, IUnitOfWork work, TimeProvider clock) : IRequestHandler<CheckInWalkInCommand, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(CheckInWalkInCommand request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        await using var transaction = await work.BeginTransactionAsync(ct);
        var plate = GateRules.NormalizePlate(request.PlateNumber);
        await guards.LockVehicleAsync(plate, ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        if (await sessions.HasActivePlateAsync(scope.TenantId, request.SiteId, plate, ct))
            throw new ReservationGateException(409, "VEHICLE_ALREADY_INSIDE", "An active parking session already exists for this plate.");
        var now = clock.GetUtcNow();
        var id = Guid.NewGuid();
        var session = new ParkingSessionRow { Id = id, TenantId = scope.TenantId, SiteId = request.SiteId,
            OriginalPlate = request.PlateNumber.Trim(), NormalizedPlate = plate,
            VehicleType = GateRules.NormalizeVehicleType(request.VehicleType) ?? throw new InvalidOperationException("Vehicle type was not validated."),
            TicketReference = $"WALKIN-{id:N}", EntryTime = now, EntryImageUrl = request.ImageData,
            SessionStatus = "ACTIVE", CreatedAt = now, Version = 1 };
        await sessions.AddAsync(session, ct);
        await audit.PublishAsync(GateProjection.Audit(scope, null, session.Id, "VEHICLE_CHECK_IN", now), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return GateProjection.WalkIn(session);
    }
}

public sealed class CheckOutWalkInCommandHandler(IOperatorIdentityClient identity, IParkingSessionRepository sessions, IOperationsAuditPublisher audit,
    ISlotAllocationRepository allocations, IOperationsConcurrency guards, IUnitOfWork work, TimeProvider clock, IOperatorGateSlotClaims gateSlots)
    : IRequestHandler<CheckOutWalkInCommand, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(CheckOutWalkInCommand request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(GateRules.NormalizePlate(request.PlateNumber), ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var session = await sessions.FindForUpdateAsync(scope.TenantId, request.SiteId, request.SessionId, ct);
        if (session is null || session.ReservationId is not null)
            throw new ReservationGateException(404, "WALK_IN_SESSION_NOT_FOUND", "Walk-in session was not found.");
        GateRules.VerifyPlate(session.OriginalPlate ?? session.NormalizedPlate ?? "", request.PlateNumber);
        if (session.ExitTime is null)
        {
            var now = clock.GetUtcNow();
            await GateDeparture.RecordAsync(session, allocations, now, request.ImageData, ct);
            await audit.PublishAsync(GateProjection.Audit(scope, null, session.Id, "VEHICLE_CHECK_OUT", now), ct);
            await work.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        await GateDeparture.ReleaseScannedSlotAsync(session, gateSlots, ct);
        return GateProjection.WalkIn(session);
    }
}

public sealed class CheckInReservationCommandHandler(IOperatorIdentityClient identity, IReservationRepository reservations,
    IParkingSessionRepository sessions, ISlotAllocationRepository allocations, IOperationsAuditPublisher audit,
    IOperationsConcurrency guards, IUnitOfWork work, TimeProvider clock) : IRequestHandler<CheckInReservationCommand, ReservationGateEntry>
{
    public async Task<ReservationGateEntry> Handle(CheckInReservationCommand request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var observed = await reservations.FindByIdAsync(scope.TenantId, request.SiteId, request.ReservationId, ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found at this site.");
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(GateRules.NormalizePlate(observed.LicensePlate), ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var reservation = await reservations.FindForUpdateAsync(scope.TenantId, request.SiteId, request.ReservationId, ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found at this site.");
        var plate = reservation.OriginalPlate ?? reservation.NormalizedPlate ?? "";
        GateRules.VerifyPlate(plate, request.PlateNumber);
        var allocation = await allocations.FindReservedAsync(scope.TenantId, request.SiteId, reservation.Id, ct)
            ?? throw new ReservationGateException(409, "ALLOCATION_UNAVAILABLE", "A live allocation is required before entering parking.");
        var now = clock.GetUtcNow();
        if (allocation.AllocatedStartTime > now || allocation.AllocatedEndTime <= now)
            throw new ReservationGateException(409, "ALLOCATION_UNAVAILABLE", "The allocation is not usable at this time.");
        if (await sessions.HasActivePlateAsync(scope.TenantId, request.SiteId, GateRules.NormalizePlate(plate), ct))
            throw new ReservationGateException(409, "VEHICLE_ALREADY_INSIDE", "An active parking session already exists for this plate.");
        var sessionId = Guid.NewGuid();
        reservation.EnterParking();
        allocation.AttachToSession(sessionId);
        await sessions.AddAsync(new ParkingSessionRow { Id = sessionId, ReservationId = reservation.Id,
            TenantId = scope.TenantId, SiteId = request.SiteId, AccountId = reservation.AccountId,
            OriginalPlate = plate, NormalizedPlate = GateRules.NormalizePlate(plate),
            VehicleType = reservation.VehicleType, TicketReference = $"GATE-{sessionId:N}", EntryTime = now,
            EntryImageUrl = request.ImageData, SessionStatus = "ACTIVE", Version = 1, CreatedAt = now }, ct);
        await audit.PublishAsync(GateProjection.Audit(scope, reservation.Id, sessionId, "VEHICLE_CHECK_IN", now), ct);
        await work.SaveChangesAsync(ct);
        var result = await reservations.FindByIdAsync(scope.TenantId, request.SiteId, reservation.Id, ct)
            ?? throw new InvalidOperationException("Saved reservation is missing.");
        await transaction.CommitAsync(ct);
        return result;
    }
}

public sealed class CheckOutReservationCommandHandler(IOperatorIdentityClient identity, IReservationRepository reservations,
    IParkingSessionRepository sessions, ISlotAllocationRepository allocations, IOperationsAuditPublisher audit,
    IOperationsConcurrency guards, IUnitOfWork work, TimeProvider clock, IOperatorGateSlotClaims gateSlots)
    : IRequestHandler<CheckOutReservationCommand, ReservationGateEntry>
{
    // This legacy Gate command records observed physical departure; payment preparation is a separate command.
    public async Task<ReservationGateEntry> Handle(CheckOutReservationCommand request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var observed = await reservations.FindByIdAsync(scope.TenantId, request.SiteId, request.ReservationId, ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found at this site.");
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(GateRules.NormalizePlate(observed.LicensePlate), ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var reservation = await reservations.FindForUpdateAsync(scope.TenantId, request.SiteId, request.ReservationId, ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found at this site.");
        GateRules.VerifyPlate(reservation.OriginalPlate ?? reservation.NormalizedPlate ?? "", request.PlateNumber);
        var departed = reservation.Status == "COMPLETED";
        var session = await sessions.FindByReservationAsync(scope.TenantId, request.SiteId, reservation.Id, !departed, ct)
            ?? throw new ReservationGateException(409, "ACTIVE_SESSION_NOT_FOUND", "Parking session evidence is missing.");
        if (!departed)
        {
            var now = clock.GetUtcNow();
            reservation.CompleteParking();
            await GateDeparture.RecordAsync(session, allocations, now, request.ImageData, ct);
            await audit.PublishAsync(GateProjection.Audit(scope, reservation.Id, session.Id, "VEHICLE_CHECK_OUT", now), ct);
            await work.SaveChangesAsync(ct);
        }
        var result = await reservations.FindByIdAsync(scope.TenantId, request.SiteId, reservation.Id, ct)
            ?? throw new InvalidOperationException("Saved reservation is missing.");
        await transaction.CommitAsync(ct);
        await GateDeparture.ReleaseScannedSlotAsync(session, gateSlots, ct);
        return result;
    }
}

internal static class GateDeparture
{
    public static async Task RecordAsync(ParkingSessionRow session, ISlotAllocationRepository allocations,
        DateTimeOffset departedAt, string? imageData, CancellationToken ct)
    {
        session.RecordDeparture(departedAt, imageData);
        foreach (var allocation in await allocations.FindActiveForSessionAsync(session.TenantId, session.SiteId, session.Id, ct))
            allocation.Release(departedAt);
    }

    public static Task ReleaseScannedSlotAsync(ParkingSessionRow session, IOperatorGateSlotClaims gateSlots, CancellationToken ct) =>
        session.LocationScannedAt is not null && session.CurrentSlotId is { } slotId
            ? gateSlots.ReleaseAsync(session.TenantId, session.SiteId, slotId, ct)
            : Task.CompletedTask;
}

internal static class GateProjection
{
    public static WalkInParkingSession WalkIn(ParkingSessionRow session) => new(session.Id, session.SiteId,
        session.CurrentSlotId, session.CurrentSlotId?.ToString(), session.OriginalPlate ?? session.NormalizedPlate ?? "",
        session.VehicleType, session.EntryTime, session.ExitTime, session.EntryImageUrl, session.ExitImageUrl);
    public static OperationsGateAuditV1 Audit(OperatorSiteScope scope, Guid? reservationId, Guid sessionId, string action, DateTimeOffset now) =>
        new(Guid.NewGuid(), now, scope.UserId, scope.TenantId, scope.SiteId, action, reservationId, sessionId);
}
