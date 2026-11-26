using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record SavePassPlanDraftRequest(
    [property: JsonPropertyName("zoneId")] Guid? ZoneId,
    [property: JsonPropertyName("vehicleTypes")] IReadOnlyList<string>? VehicleTypes,
    [property: JsonPropertyName("ordinaryCoverageRules")] OrdinaryCoverageRulesDto? OrdinaryCoverageRules,
    [property: JsonPropertyName("isAvailableOnly")] bool? IsAvailableOnly);
