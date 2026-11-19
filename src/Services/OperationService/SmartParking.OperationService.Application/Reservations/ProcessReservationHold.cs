using MediatR;
namespace SmartParking.OperationService.Application.Reservations;

public sealed record ProcessReservationHold(ReservationHoldChangedV1 Fact) : IRequest;
