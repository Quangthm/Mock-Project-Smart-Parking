using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record AdmitQuoteReservation(QuoteReservationIntent Intent, Guid IdempotencyKey) : IRequest<ReservationReceipt>;
