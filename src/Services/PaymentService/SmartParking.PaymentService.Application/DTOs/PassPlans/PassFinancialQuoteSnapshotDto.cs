using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record PassFinancialQuoteSnapshotDto(
    [property: JsonPropertyName("financialTermId")] Guid FinancialTermId,
    [property: JsonPropertyName("financialTermRevision")] int FinancialTermRevision,
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("fullPeriodPrice")] decimal FullPeriodPrice,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("refundPolicySnapshot")] PassRefundPolicyDto RefundPolicySnapshot,
    [property: JsonPropertyName("quotedAt")] DateTimeOffset QuotedAt);
