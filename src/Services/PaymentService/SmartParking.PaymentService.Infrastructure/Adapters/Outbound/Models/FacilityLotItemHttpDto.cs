using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound.Models;

internal sealed record FacilityLotItemHttpDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId);
