using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityValidateSiteRequestDto(
    [property: JsonPropertyName("siteIds")] Guid[] SiteIds,
    [property: JsonPropertyName("tenantIds")] Guid[] TenantIds);
