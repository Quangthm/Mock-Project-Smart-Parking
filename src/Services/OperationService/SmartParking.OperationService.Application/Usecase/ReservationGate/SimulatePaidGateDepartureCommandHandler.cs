using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record SimulatePaidGateDepartureCommand(
    Guid TenantId, Guid SiteId, string ReferenceType, Guid ReferenceId, Guid PaymentOrderId, DateTimeOffset OccurredAt) : IRequest;

public sealed class SimulatePaidGateDepartureCommandHandler(
    IParkingSessionRepository sessions,
    IReservationRepository reservations,
    ISlotAllocationRepository allocations,
    IOperationsConcurrency guards,
    IUnitOfWork work,
    IOperatorGateSlotClaims gateSlots) : IRequestHandler<SimulatePaidGateDepartureCommand>
{
    public async Task Handle(SimulatePaidGateDepartureCommand request, CancellationToken ct)
    {
        var observed = request.ReferenceType switch
        {
            "PARKING_SESSION" => await sessions.FindForUpdateAsync(request.TenantId, request.SiteId, request.ReferenceId, ct),
            "RESERVATION" => await sessions.FindByReservationAsync(request.TenantId, request.SiteId, request.ReferenceId, true, ct)
                ?? await sessions.FindByReservationAsync(request.TenantId, request.SiteId, request.ReferenceId, false, ct),
            _ => null
        };
        if (observed is null || observed.CheckoutPaymentOrderId != request.PaymentOrderId || request.PaymentOrderId == Guid.Empty)
            return;

        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(request.SiteId, ct);
        var session = await sessions.FindForUpdateAsync(request.TenantId, request.SiteId, observed.Id, ct);
        if (session is null || !session.RecordCheckoutSettlement(request.PaymentOrderId, request.OccurredAt))
            return;

        if (session.ExitTime is null)
        {
            if (session.ReservationId is { } reservationId)
            {
                var reservation = await reservations.FindForUpdateAsync(request.TenantId, request.SiteId, reservationId, ct)
                    ?? throw new InvalidOperationException("The paid parking session has no reservation.");
                reservation.CompleteParking();
            }
            await GateDeparture.RecordAsync(session, allocations, session.CheckoutPaidAt!.Value, null, ct);
        }
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await GateDeparture.ReleaseScannedSlotAsync(session, gateSlots, ct);
    }
}
