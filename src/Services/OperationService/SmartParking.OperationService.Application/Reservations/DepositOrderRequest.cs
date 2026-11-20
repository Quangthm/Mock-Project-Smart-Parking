using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record DepositOrderRequest(Guid OperationId, Guid ReservationId, Guid AccountId,
    Guid TenantId, Guid SiteId, Guid QuoteId, [property: JsonPropertyName("termsVersion")] string QuoteVersion,
    decimal Amount, string Currency, string PaymentMethod, DateTimeOffset ExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ZeroQuoteSelection? ZeroSelection = null);
