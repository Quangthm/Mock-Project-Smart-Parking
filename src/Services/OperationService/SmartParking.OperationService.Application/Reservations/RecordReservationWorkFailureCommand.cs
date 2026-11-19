using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record RecordReservationWorkFailureCommand(Guid RequestId) : IRequest;
