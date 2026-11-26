using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record CheckAdmissionDeadline(Guid RequestId, DateTimeOffset ExpectedAt) : IRequest<ReservationDeadlineResult>;
