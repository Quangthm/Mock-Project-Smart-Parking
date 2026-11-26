using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record QuotePassFinancialTermsRequest(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("planRevision")] int PlanRevision,
    [property: JsonPropertyName("expectedFinancialTermRevision")] int ExpectedFinancialTermRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("vehicleType")] string VehicleType);
