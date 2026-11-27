using MediatR;
namespace SmartParking.OperationService.Application.Reservations;

public sealed record ExpireReservationHoldCommand(Guid ReservationId, DateTimeOffset ExpectedAt) : IRequest<ReservationDeadlineResult>;
