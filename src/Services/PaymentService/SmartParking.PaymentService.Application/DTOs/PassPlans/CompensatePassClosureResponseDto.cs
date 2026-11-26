using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record CompensatePassClosureResponseDto(
    [property: JsonPropertyName("compensationId")] Guid CompensationId,
    [property: JsonPropertyName("refundRequestId")] Guid? RefundRequestId,
    [property: JsonPropertyName("closureId")] Guid ClosureId,
    [property: JsonPropertyName("closureType")] string ClosureType,
    [property: JsonPropertyName("passId")] Guid PassId,
    [property: JsonPropertyName("paymentOrderId")] Guid PaymentOrderId,
    [property: JsonPropertyName("newlyCompensatedAmount")] decimal NewlyCompensatedAmount,
    [property: JsonPropertyName("totalOrderCompensatedAmount")] decimal TotalOrderCompensatedAmount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("compensatedIntervals")] IReadOnlyList<ClosureIntervalDto> CompensatedIntervals,
    [property: JsonPropertyName("isReplayed")] bool IsReplayed);
