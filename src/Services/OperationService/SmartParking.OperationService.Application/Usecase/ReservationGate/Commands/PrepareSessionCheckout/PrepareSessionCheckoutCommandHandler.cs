using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class PrepareSessionCheckoutCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store,
    ICheckoutPaymentClient payment) : IRequestHandler<PrepareSessionCheckoutCommand, CheckoutPaymentPreparation>
{
    public async Task<CheckoutPaymentPreparation> Handle(
        PrepareSessionCheckoutCommand request,
        CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var session = await store.FindActiveWalkInAsync(
            scope.TenantId,
            request.SiteId,
            request.PlateNumber ?? string.Empty,
            ct);
        if (session is null || session.Id != request.SessionId)
            throw new ReservationGateException(404, "PARKING_SESSION_NOT_FOUND", "Parking session was not found.");

        return await payment.PrepareAsync(
            scope.TenantId,
            request.SiteId,
            session.Id,
            "PARKING_SESSION",
            session.VehicleType,
            session.EntryTime,
            DateTimeOffset.UtcNow,
            request.PlateNumber ?? session.LicensePlate,
            null,
            ct);
    }
}