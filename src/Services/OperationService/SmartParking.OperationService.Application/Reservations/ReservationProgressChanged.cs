namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationProgressChanged(Guid EventId, Guid RequestId, Guid AccountId);
