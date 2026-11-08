using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record FindReservationQuery(Guid SiteId, string Query) : IRequest<ReservationGateEntry>;