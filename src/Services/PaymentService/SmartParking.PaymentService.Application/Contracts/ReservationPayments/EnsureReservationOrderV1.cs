using System.Text.Json.Serialization;

namespace SmartParking.Contracts.ReservationPayments;

public sealed record EnsureReservationOrderV1(Guid ReservationId, Guid AccountId, Guid TenantId, Guid SiteId,
    Guid QuoteId, Guid RequestedSlotId, string CanonicalPlate, DateTimeOffset Start, DateTimeOffset End,
    string PaymentMethod, [property: JsonRequired] decimal DepositAmount, string Currency,
    [property: JsonRequired] bool IsCovered, string CoverageVersion, DateTimeOffset? HoldExpiresAt);
