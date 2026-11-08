using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckOutReservationCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<CheckOutReservationCommand, ReservationGateEntry>
{
    public async Task<ReservationGateEntry> Handle(CheckOutReservationCommand request, CancellationToken ct)
    {
        GateRequestValidation.ValidateReservationAction(request.PlateNumber, request.ImageData);
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.CheckOutAsync(
            scope.TenantId, request.SiteId, scope.UserId, request.ReservationId, request.PlateNumber, request.ImageData, ct);
    }
}