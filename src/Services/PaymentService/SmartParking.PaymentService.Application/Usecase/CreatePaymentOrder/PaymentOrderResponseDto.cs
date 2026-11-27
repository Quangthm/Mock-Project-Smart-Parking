namespace SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;

public sealed record PaymentOrderResponseDto(
    Guid OrderId,
    string OrderCode,
    decimal Amount,
    string Status,
    DateTimeOffset ExpiresAt);
