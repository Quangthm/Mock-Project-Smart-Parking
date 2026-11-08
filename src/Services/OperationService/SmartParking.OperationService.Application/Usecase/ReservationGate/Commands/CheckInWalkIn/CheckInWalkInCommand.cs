using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record CheckInWalkInCommand(Guid SiteId, string PlateNumber, string? ImageData, string? VehicleType)
    : IRequest<WalkInParkingSession>;