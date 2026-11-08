using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record ListParkingSessionsQuery(Guid SiteId) : IRequest<IReadOnlyList<ParkingSessionHistoryEntry>>;