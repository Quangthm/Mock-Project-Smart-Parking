namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record PaymentEnvelope<T>(bool Success, T Data);
