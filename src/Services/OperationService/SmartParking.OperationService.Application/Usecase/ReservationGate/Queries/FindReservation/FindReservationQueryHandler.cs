using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class FindReservationQueryHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<FindReservationQuery, ReservationGateEntry>
{
    public async Task<ReservationGateEntry> Handle(FindReservationQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 100)
            throw new ReservationGateException(400, "INVALID_QUERY", "Enter a reservation ID or plate.");

        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.FindAsync(scope.TenantId, request.SiteId, request.Query.Trim(), ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "No matching reservation was found.");
    }
}