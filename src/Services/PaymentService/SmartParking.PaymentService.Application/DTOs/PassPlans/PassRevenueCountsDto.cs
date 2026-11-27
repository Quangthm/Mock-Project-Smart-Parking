using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record PassRevenueCountsDto(
    [property: JsonPropertyName("fulfilledPasses")] int FulfilledPasses,
    [property: JsonPropertyName("awaitingRefundOrders")] int AwaitingRefundOrders,
    [property: JsonPropertyName("completedRefunds")] int CompletedRefunds);
