using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record RecordReservationDepositCommand(ReservationDepositEvidence Evidence) : IRequest;
