namespace SmartParking.Contracts.ReservationPayments;

public sealed record ReservationCheckoutChangedV1(Guid AccountId, Guid ReservationId);
