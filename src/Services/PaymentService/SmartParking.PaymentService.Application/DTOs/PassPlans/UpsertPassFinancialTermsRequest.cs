using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record UpsertPassFinancialTermsRequest(
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("vehiclePrices")] IReadOnlyList<PassVehiclePriceDto> VehiclePrices,
    [property: JsonPropertyName("refundPolicy")] PassRefundPolicyDto? RefundPolicy);
