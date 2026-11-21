using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ConfirmZeroPriceReservationCommand(Guid ReservationId) : IRequest;
