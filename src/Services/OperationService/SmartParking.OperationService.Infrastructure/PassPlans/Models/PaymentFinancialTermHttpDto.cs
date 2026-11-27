using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record PaymentFinancialTermHttpDto(
    [property: JsonPropertyName("financialTermId")] Guid FinancialTermId,
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("financialTermRevision")] int FinancialTermRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("vehiclePrices")] IReadOnlyList<PaymentFinancialTermVehiclePriceHttpDto>? VehiclePrices,
    [property: JsonPropertyName("refundPolicy")] PaymentFinancialTermRefundPolicyHttpDto? RefundPolicy,
    [property: JsonPropertyName("effectiveFrom")] DateTimeOffset EffectiveFrom);
