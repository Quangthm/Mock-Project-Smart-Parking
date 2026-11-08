using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CheckInWalkInCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<CheckInWalkInCommand, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(CheckInWalkInCommand request, CancellationToken ct)
    {
        GateRequestValidation.ValidatePlate(request.PlateNumber, "Enter a valid vehicle plate.");
        GateRequestValidation.ValidateImageData(request.ImageData);
        var vehicleType = GateRequestValidation.NormalizeVehicleType(request.VehicleType);
        if (vehicleType is null)
            throw new ReservationGateException(400, "INVALID_VEHICLE_TYPE", "Vehicle type must be CAR or MOTORCYCLE.");

        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.CreateWalkInAsync(
            scope.TenantId, request.SiteId, Guid.NewGuid(), vehicleType, request.PlateNumber, request.ImageData, ct);
    }
}