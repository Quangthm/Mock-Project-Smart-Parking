using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.Reservations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QuoteReservationIntent(string LicensePlate, Guid RequestedSlotId, DateTimeOffset Start,
    DateTimeOffset End, string PaymentMethod, Guid QuoteId, string QuoteVersion);
