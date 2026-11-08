using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record CheckInReservationCommand(Guid SiteId, Guid ReservationId, string? PlateNumber, string? ImageData)
    : IRequest<ReservationGateEntry>;