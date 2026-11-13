using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ParkingSessions.Queries.GetDriverParkingHistory;

public sealed record GetDriverParkingHistoryQuery(Guid AccountId, string? StatusFilter, int Limit = 50)
    : IRequest<IReadOnlyList<ParkingSessionHistoryEntry>>;

