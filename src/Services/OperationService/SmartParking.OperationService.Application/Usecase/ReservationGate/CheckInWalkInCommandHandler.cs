using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckInWalkInCommandHandler(
    IOperatorIdentityClient identity,
    IParkingSessionRepository sessions,
    IOperationsAuditPublisher audit,
    GateSlotOccupancy occupancy,
    IOperationsConcurrency guards,
    IUnitOfWork work,
    TimeProvider clock) : IRequestHandler<CheckInWalkInCommand, WalkInParkingSession>
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

        var vehicleType = GateRules.NormalizeVehicleType(request.VehicleType)
            ?? throw new InvalidOperationException("Vehicle type was not validated.");
        var now = clock.GetUtcNow();
        var id = Guid.NewGuid();
        var session = new ParkingSessionRow
        {
            Id = id,
            TenantId = scope.TenantId,
            SiteId = request.SiteId,
            OriginalPlate = request.PlateNumber.Trim(),
            NormalizedPlate = plate,
            VehicleType = vehicleType,
            TicketReference = $"WALKIN-{id:N}",
            EntryTime = now,
            EntryImageUrl = request.ImageData,
            SessionStatus = "ACTIVE",
            CreatedAt = now,
            Version = 1
        };
        await using var lease = request.PreferredSlotId is Guid slotId
            ? await occupancy.ClaimAsync(session, slotId, now, ct)
            : null;
        if (lease is not null)
            session.RecordSlotScan(lease.Slot.Id, clock.GetUtcNow());

        await sessions.AddAsync(session, ct);
        await audit.PublishAsync(GateProjection.Audit(scope, null, session.Id, "VEHICLE_CHECK_IN", now), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        lease?.Commit();
        return GateProjection.WalkIn(session, lease?.Slot.Code);
    }
}
