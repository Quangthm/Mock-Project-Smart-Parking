using MediatR;

namespace SmartParking.PaymentService.Application.Reservations;

public sealed record RecordZeroDisposition(Guid OperationId, Guid ReservationId, Guid AccountId, Guid OrderId, Guid ReceiptId,
    Guid QuoteId, string Reason) : IRequest<Guid>;
