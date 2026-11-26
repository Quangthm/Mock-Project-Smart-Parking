using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityValidatedSiteDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId);
