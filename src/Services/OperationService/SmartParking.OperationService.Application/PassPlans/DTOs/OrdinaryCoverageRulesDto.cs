using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record OrdinaryCoverageRulesDto(
    [property: JsonPropertyName("dayCovered")] bool DayCovered,
    [property: JsonPropertyName("nightCovered")] bool NightCovered,
    [property: JsonPropertyName("excludedZoneIds")] IReadOnlyList<Guid> ExcludedZoneIds);
