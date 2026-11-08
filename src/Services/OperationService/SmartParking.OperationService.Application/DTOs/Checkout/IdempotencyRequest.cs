namespace SmartParking.OperationService.Application.DTOs.Checkout;

public sealed record IdempotencyRequest(string IdempotencyKey);