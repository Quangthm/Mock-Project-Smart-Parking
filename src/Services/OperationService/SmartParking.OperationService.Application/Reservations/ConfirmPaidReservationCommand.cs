using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ConfirmPaidReservationCommand(Guid ReservationId, Guid CollectionId) : IRequest;
