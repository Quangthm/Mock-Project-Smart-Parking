using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class ListReservationsQueryHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<ListReservationsQuery, IReadOnlyList<ReservationGateEntry>>
{
    public async Task<IReadOnlyList<ReservationGateEntry>> Handle(ListReservationsQuery request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.ListAsync(scope.TenantId, request.SiteId, ct);
    }
}