using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record PassFinancialTermLookupItemDto(
    [property: JsonPropertyName("financialTermId")] Guid FinancialTermId,
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("financialTermRevision")] int FinancialTermRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("vehiclePrices")] IReadOnlyList<PassVehiclePriceDto> VehiclePrices,
    [property: JsonPropertyName("refundPolicy")] PassRefundPolicyDto RefundPolicy,
    [property: JsonPropertyName("effectiveFrom")] DateTimeOffset EffectiveFrom);
