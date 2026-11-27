using MediatR;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record RequireBookingPermissionCommand(Guid AccountId, EffectiveReservationPolicy Policy,
    DateTimeOffset EvaluatedAt) : IRequest;
