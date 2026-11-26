using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record UpsertPassFinancialTermsResponseDto(
    [property: JsonPropertyName("financialTermId")] Guid FinancialTermId,
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("financialTermRevision")] int FinancialTermRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("vehiclePrices")] IReadOnlyList<PassVehiclePriceDto> VehiclePrices,
    [property: JsonPropertyName("effectiveFrom")] DateTimeOffset EffectiveFrom,
    [property: JsonIgnore] bool IsReplayed = false);
