using System.Text.Json.Serialization;

namespace SmartParking.Contracts.ReservationPayments;

public sealed record ReservationZeroReceiptIssuedV1(Guid ReservationId, Guid AccountId, Guid TenantId, Guid SiteId,
    Guid QuoteId, Guid PaymentOrderId, Guid ReceiptId, [property: JsonRequired] decimal DepositAmount, string Currency);
