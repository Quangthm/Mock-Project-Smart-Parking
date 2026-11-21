namespace SmartParking.PaymentService.Application.DTOs.Reservations;

public sealed record EnsureReservationDepositOrderResponseDto(
    string State,
    Guid? OrderId,
    EnsureReservationDepositOrderRequestDto AcceptedRequest,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid? ReceiptId = null
);
