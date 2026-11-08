using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class FindWalkInSessionQueryHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<FindWalkInSessionQuery, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(FindWalkInSessionQuery request, CancellationToken ct)
    {
        GateRequestValidation.ValidatePlate(request.Plate, "Enter a valid vehicle plate.");
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.FindActiveWalkInAsync(scope.TenantId, request.SiteId, request.Plate, ct)
            ?? throw new ReservationGateException(404, "WALK_IN_SESSION_NOT_FOUND", "No active walk-in session was found for this plate.");
    }
}