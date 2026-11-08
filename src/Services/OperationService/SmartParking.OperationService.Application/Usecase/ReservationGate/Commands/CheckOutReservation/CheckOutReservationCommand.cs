using MediatR;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record CheckOutReservationCommand(Guid SiteId, Guid ReservationId, string? PlateNumber, string? ImageData)
    : IRequest<ReservationGateEntry>;