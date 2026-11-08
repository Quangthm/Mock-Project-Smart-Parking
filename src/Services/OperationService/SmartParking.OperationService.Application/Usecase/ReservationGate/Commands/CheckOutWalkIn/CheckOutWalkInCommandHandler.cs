using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckOutWalkInCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<CheckOutWalkInCommand, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(CheckOutWalkInCommand request, CancellationToken ct)
    {
        GateRequestValidation.ValidatePlate(request.PlateNumber, "Enter a valid vehicle plate.");
        GateRequestValidation.ValidateImageData(request.ImageData);
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.CheckOutWalkInAsync(
            scope.TenantId, request.SiteId, request.SessionId, request.PlateNumber, request.ImageData, ct);
    }
}