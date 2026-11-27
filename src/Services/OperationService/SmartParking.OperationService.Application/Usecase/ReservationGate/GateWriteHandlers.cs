using MediatR;
using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

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

internal static class GateProjection
{
    public static WalkInParkingSession WalkIn(ParkingSessionRow session, string? slotCode = null) => new(session.Id, session.SiteId,
        session.CurrentSlotId, slotCode ?? session.CurrentSlotId?.ToString(), session.OriginalPlate ?? session.NormalizedPlate ?? "",
        session.VehicleType, session.EntryTime, session.ExitTime, session.EntryImageUrl, session.ExitImageUrl);
    public static OperationsGateAuditV1 Audit(OperatorSiteScope scope, Guid? reservationId, Guid sessionId, string action, DateTimeOffset now) =>
        new(Guid.NewGuid(), now, scope.UserId, scope.TenantId, scope.SiteId, action, reservationId, sessionId);
}
