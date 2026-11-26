using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityLotSummaryDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status);
