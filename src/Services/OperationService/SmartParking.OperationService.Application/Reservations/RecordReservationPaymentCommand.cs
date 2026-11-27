using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record RecordReservationPaymentCommand(ReservationPaymentEvidence Evidence) : IRequest;
