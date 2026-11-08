using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record CheckOutWalkInCommand(Guid SiteId, Guid SessionId, string PlateNumber, string? ImageData)
    : IRequest<WalkInParkingSession>;