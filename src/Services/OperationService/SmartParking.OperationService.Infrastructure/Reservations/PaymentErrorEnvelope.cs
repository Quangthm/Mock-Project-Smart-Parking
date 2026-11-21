namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record PaymentErrorEnvelope(string? ErrorCode, string? Message);
