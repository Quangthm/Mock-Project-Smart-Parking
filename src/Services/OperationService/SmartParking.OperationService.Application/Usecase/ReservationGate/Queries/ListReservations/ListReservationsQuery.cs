using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record ListReservationsQuery(Guid SiteId) : IRequest<IReadOnlyList<ReservationGateEntry>>;