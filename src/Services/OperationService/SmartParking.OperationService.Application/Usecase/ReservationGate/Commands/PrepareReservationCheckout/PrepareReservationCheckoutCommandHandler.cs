using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class PrepareReservationCheckoutCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store,
    ICheckoutPaymentClient payment) : IRequestHandler<PrepareReservationCheckoutCommand, CheckoutPaymentPreparation>
{
    public async Task<CheckoutPaymentPreparation> Handle(
        PrepareReservationCheckoutCommand request,
        CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var reservation = await store.FindAsync(scope.TenantId, request.SiteId, request.ReservationId.ToString(), ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found.");
        if (reservation.Id != request.ReservationId)
            throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found.");

        return await payment.PrepareAsync(
            scope.TenantId,
            request.SiteId,
            reservation.Id,
            "RESERVATION",
            reservation.VehicleType,
            reservation.EntryTime ?? reservation.StartTime,
            DateTimeOffset.UtcNow,
            request.PlateNumber ?? reservation.LicensePlate,
            reservation.AccountId,
            ct);
    }
}