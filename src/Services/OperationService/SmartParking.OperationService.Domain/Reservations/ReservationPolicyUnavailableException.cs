namespace SmartParking.OperationService.Domain.Reservations;

public sealed class ReservationPolicyUnavailableException(string message) : Exception(message);
