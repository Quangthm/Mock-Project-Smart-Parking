using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReadOwnReservationProgress(Guid RequestId) : IRequest<ReservationProgress>;
