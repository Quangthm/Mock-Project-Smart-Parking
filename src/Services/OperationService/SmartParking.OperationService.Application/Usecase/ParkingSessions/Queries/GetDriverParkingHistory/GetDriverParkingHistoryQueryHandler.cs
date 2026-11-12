using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ParkingSessions.Queries.GetDriverParkingHistory;

public sealed class GetDriverParkingHistoryQueryHandler(IParkingSessionRepository repository)
    : IRequestHandler<GetDriverParkingHistoryQuery, IReadOnlyList<ParkingSessionHistoryEntry>>
{
    public async Task<IReadOnlyList<ParkingSessionHistoryEntry>> Handle(
        GetDriverParkingHistoryQuery request,
        CancellationToken ct)
    {
        return await repository.ListSessionsByAccountAsync(
            request.AccountId,
            request.StatusFilter,
            request.Limit,
            ct);
    }
}

