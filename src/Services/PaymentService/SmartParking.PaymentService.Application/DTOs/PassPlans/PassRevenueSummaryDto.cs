using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record PassRevenueSummaryDto(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("collectedAmount")] decimal CollectedAmount,
    [property: JsonPropertyName("collectedGrantedAmount")] decimal CollectedGrantedAmount,
    [property: JsonPropertyName("collectedAwaitingRefundAmount")] decimal CollectedAwaitingRefundAmount,
    [property: JsonPropertyName("pendingRefundAmount")] decimal PendingRefundAmount,
    [property: JsonPropertyName("refundedAmount")] decimal RefundedAmount,
    [property: JsonPropertyName("netSettledRevenue")] decimal NetSettledRevenue,
    [property: JsonPropertyName("counts")] PassRevenueCountsDto Counts);
