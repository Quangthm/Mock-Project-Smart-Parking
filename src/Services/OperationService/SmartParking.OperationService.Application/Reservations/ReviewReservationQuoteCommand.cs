using System.Text.Json.Serialization;
using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewReservationQuoteCommand(
    string LicensePlate, Guid RequestedSlotId, DateTimeOffset Start, DateTimeOffset End, string PaymentMethod)
    : IRequest<ReviewedReservationQuote>;
