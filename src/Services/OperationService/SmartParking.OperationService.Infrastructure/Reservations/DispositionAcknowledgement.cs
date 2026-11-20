namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record DispositionAcknowledgement(Guid RefundRequestId, Guid ReservationId, Guid CollectionId, string Status);
