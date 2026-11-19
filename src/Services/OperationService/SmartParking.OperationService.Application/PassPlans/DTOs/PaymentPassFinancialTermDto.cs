using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record PaymentPassFinancialTermDto(
    [property: JsonPropertyName("financialTermId")] Guid FinancialTermId,
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("financialTermRevision")] int FinancialTermRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("vehiclePrices")] IReadOnlyList<PaymentPassVehiclePriceDto> VehiclePrices,
    [property: JsonPropertyName("refundPolicy")] PaymentPassRefundPolicyDto RefundPolicy,
    [property: JsonPropertyName("effectiveFrom")] DateTimeOffset EffectiveFrom);
