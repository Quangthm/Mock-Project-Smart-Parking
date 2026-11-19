using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound.Models;

internal sealed record UserOwnerScopeHttpDto(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("tenantIds")] IReadOnlyList<Guid>? TenantIds);
