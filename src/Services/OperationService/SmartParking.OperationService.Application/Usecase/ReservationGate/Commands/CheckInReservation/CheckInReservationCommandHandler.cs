using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckInReservationCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<CheckInReservationCommand, ReservationGateEntry>
{
    public async Task<ReservationGateEntry> Handle(CheckInReservationCommand request, CancellationToken ct)
    {
        GateRequestValidation.ValidateReservationAction(request.PlateNumber, request.ImageData);
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.CheckInAsync(
            scope.TenantId, request.SiteId, scope.UserId, request.ReservationId, request.PlateNumber, request.ImageData, ct);
    }
}