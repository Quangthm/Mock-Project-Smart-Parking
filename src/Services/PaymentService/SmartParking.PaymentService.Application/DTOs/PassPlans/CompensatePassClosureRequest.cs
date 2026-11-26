using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record CompensatePassClosureRequest(
    [property: JsonPropertyName("idempotencyKey")] string IdempotencyKey,
    [property: JsonPropertyName("closureId")] Guid ClosureId,
    [property: JsonPropertyName("closureType")] string ClosureType,
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("passId")] Guid PassId,
    [property: JsonPropertyName("paymentOrderId")] Guid PaymentOrderId,
    [property: JsonPropertyName("paidPeriodStart")] DateTimeOffset PaidPeriodStart,
    [property: JsonPropertyName("paidPeriodEnd")] DateTimeOffset PaidPeriodEnd,
    [property: JsonPropertyName("unavailableIntervals")] IReadOnlyList<ClosureIntervalDto> UnavailableIntervals);
