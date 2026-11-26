namespace SmartParking.PaymentService.Application.Reservations;

public sealed record ReservationCheckoutState(string State, Guid? ReservationId, Guid? OrderId, DateTimeOffset? HoldExpiresAt = null);
