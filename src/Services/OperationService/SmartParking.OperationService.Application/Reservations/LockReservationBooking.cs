using MediatR;
using SmartParking.OperationService.Domain.Entities;
namespace SmartParking.OperationService.Application.Reservations;

public sealed record LockReservationBooking(Guid ReservationId) : IRequest<ReservationRow?>;
