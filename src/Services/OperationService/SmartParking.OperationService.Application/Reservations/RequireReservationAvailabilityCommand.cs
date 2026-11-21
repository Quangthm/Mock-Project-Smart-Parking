using MediatR;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record RequireReservationAvailabilityCommand(ReservationRow Proposed,
    FacilityResourceSnapshot Resources, DateTimeOffset EvaluatedAt, EffectiveReservationPolicy? ReacquisitionPolicy = null) : IRequest;
