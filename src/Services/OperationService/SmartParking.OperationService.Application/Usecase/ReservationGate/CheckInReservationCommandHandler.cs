using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckInReservationCommandHandler(IOperatorIdentityClient identity, IReservationRepository reservations,
    IParkingSessionRepository sessions, IOperationsAuditPublisher audit,
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
        var now = clock.GetUtcNow();
        if (await sessions.HasActivePlateAsync(scope.TenantId, request.SiteId, GateRules.NormalizePlate(plate), ct))
            throw new ReservationGateException(409, "VEHICLE_ALREADY_INSIDE", "An active parking session already exists for this plate.");
        var sessionId = Guid.NewGuid();
        reservation.EnterParking();
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
