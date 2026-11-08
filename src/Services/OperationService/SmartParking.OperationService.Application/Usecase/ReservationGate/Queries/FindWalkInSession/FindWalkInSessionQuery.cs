using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record FindWalkInSessionQuery(Guid SiteId, string Plate) : IRequest<WalkInParkingSession>;