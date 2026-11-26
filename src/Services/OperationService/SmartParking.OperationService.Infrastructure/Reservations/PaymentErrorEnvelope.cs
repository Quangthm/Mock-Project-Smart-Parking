namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record PaymentErrorEnvelope(string? Code, string? Message);
