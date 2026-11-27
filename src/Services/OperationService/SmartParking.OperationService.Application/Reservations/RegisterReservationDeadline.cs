namespace SmartParking.OperationService.Application.Reservations;

public sealed record RegisterReservationDeadline(ReservationDeadlineKind Kind, Guid SubjectId, DateTimeOffset ExpectedAt);
