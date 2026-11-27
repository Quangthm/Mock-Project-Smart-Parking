using System.Text.Json.Serialization;

namespace SmartParking.Contracts.ReservationPayments;

public sealed record RequestReservationDispositionV1(Guid ReservationId, Guid AccountId, Guid TenantId, Guid SiteId,
    Guid QuoteId, Guid PaymentOrderId, Guid? CollectionId, Guid? ZeroReceiptId,
    [property: JsonRequired] decimal Amount, string Currency, string Reason);
