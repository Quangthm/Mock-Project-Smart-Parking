using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record ValidateAndFreezePassPlanRequest(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("planId")] Guid PlanId,
    [property: JsonPropertyName("expectedPlanRevision")] int ExpectedPlanRevision,
    [property: JsonPropertyName("productType")] string ProductType,
    [property: JsonPropertyName("vehicleType")] string VehicleType);
