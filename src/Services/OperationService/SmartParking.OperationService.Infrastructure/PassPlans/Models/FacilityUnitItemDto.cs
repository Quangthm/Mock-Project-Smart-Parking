using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityUnitItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("parentId")] Guid? ParentId,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("maxCapacity")] int MaxCapacity);
