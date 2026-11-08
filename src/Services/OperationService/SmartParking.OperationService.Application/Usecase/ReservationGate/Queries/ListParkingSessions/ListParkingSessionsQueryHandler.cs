using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class ListParkingSessionsQueryHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<ListParkingSessionsQuery, IReadOnlyList<ParkingSessionHistoryEntry>>
{
    public async Task<IReadOnlyList<ParkingSessionHistoryEntry>> Handle(ListParkingSessionsQuery request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.ListSessionsAsync(scope.TenantId, request.SiteId, ct);
    }
}