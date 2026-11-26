using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound.Models;

internal sealed record FacilityValidatedSiteHttpDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId);
