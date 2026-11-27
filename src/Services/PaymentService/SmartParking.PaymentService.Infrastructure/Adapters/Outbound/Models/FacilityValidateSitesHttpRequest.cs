using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound.Models;

internal sealed record FacilityValidateSitesHttpRequest(
    [property: JsonPropertyName("siteIds")] IReadOnlyList<Guid> SiteIds,
    [property: JsonPropertyName("tenantIds")] IReadOnlyList<Guid> TenantIds);
