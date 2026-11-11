using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class ScanParkingSessionCommandHandler(
    IOperatorIdentityClient identity,
    IParkingSessionRepository sessions,
    IReservationRepository reservations,
    ISlotAllocationRepository allocations,
    IOperationsConcurrency guards,
    IUnitOfWork work,
    IOperatorGateSlotClaims gateSlots,
    TimeProvider clock) : IRequestHandler<ScanParkingSessionCommand, ParkingSessionHistoryEntry>
{
    public async Task<ParkingSessionHistoryEntry> Handle(ScanParkingSessionCommand request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var observed = await sessions.FindForUpdateAsync(scope.TenantId, request.SiteId, request.ReferenceId, ct)
            ?? await sessions.FindByReservationAsync(scope.TenantId, request.SiteId, request.ReferenceId, true, ct)
            ?? throw SessionNotFound();
        if (observed.ExitTime is not null)
            throw SessionNotFound();
        if (observed.LocationScannedAt is not null)
            return Project(observed);

        var reservedSlotId = observed.ReservationId is { } reservationId
            ? (await reservations.FindByIdAsync(scope.TenantId, request.SiteId, reservationId, ct))?.SlotId
                ?? throw AllocationUnavailable()
            : request.SlotId;
        await using var lease = await gateSlots.ClaimAvailableAsync(
            scope.TenantId, request.SiteId, observed.VehicleType, reservedSlotId, null, observed.ReservationId is not null, ct);

        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var session = await sessions.FindForUpdateAsync(scope.TenantId, request.SiteId, observed.Id, ct)
            ?? throw SessionNotFound();
        if (session.ExitTime is not null || session.LocationScannedAt is not null)
            throw new ReservationGateException(409, "INVALID_SESSION_STATE", "The session changed during the scan. Refresh before retrying.");
        var now = clock.GetUtcNow();
        if (lease.Slot.Id != reservedSlotId)
            throw AllocationUnavailable();

        if (session.ReservationId is { } bookedReservationId)
        {
            var active = await allocations.FindActiveForSessionAsync(scope.TenantId, request.SiteId, session.Id, ct);
            var allocation = active.SingleOrDefault(row => row.SlotId == reservedSlotId && row.ReservationId == bookedReservationId);
            if (allocation is null)
                throw AllocationUnavailable();
            if (allocation.AllocationStatus == "RESERVED")
                allocation.Occupy(session.Id);
        }
        else
        {
            var allocation = new SlotAllocationRow
            {
                Id = Guid.NewGuid(), TenantId = scope.TenantId, SiteId = request.SiteId, SlotId = reservedSlotId,
                AllocatedStartTime = now, AllocatedEndTime = DateTimeOffset.MaxValue, AllocationStatus = "RESERVED",
                CreatedAt = now
            };
            allocation.Occupy(session.Id);
            await allocations.AddAsync(allocation, ct);
        }
        session.RecordSlotScan(reservedSlotId, now);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        lease.Commit();
        return Project(session, lease.Slot.Code);
    }

    private static ParkingSessionHistoryEntry Project(ParkingSessionRow session, string? slotCode = null) =>
        new(session.Id, session.ReservationId, session.TenantId, session.SiteId, session.AccessItemId, session.AccountId,
            session.CurrentSlotId, slotCode, session.OriginalPlate, session.NormalizedPlate, session.VehicleType,
            session.TicketReference, session.EntryTime, session.EntryImageUrl, session.ExitTime, session.ExitImageUrl,
            session.SessionStatus, session.Version, session.CreatedAt, session.LocationScannedAt);

    private static ReservationGateException SessionNotFound() =>
        new(404, "PARKING_SESSION_NOT_FOUND", "Active parking session was not found.");

    private static ReservationGateException AllocationUnavailable() =>
        new(409, "ALLOCATION_UNAVAILABLE", "The assigned slot is no longer available for this session.");
}
