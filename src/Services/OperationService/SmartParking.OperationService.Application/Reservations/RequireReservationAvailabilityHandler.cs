using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class RequireReservationAvailabilityHandler(IReservationAvailabilityRepository repository)
    : IRequestHandler<RequireReservationAvailabilityCommand>
{
    public async Task Handle(RequireReservationAvailabilityCommand command, CancellationToken ct)
    {
        var proposed = command.Proposed;
        using var accepted = command.CurrentProjectionMinutes is null ? JsonDocument.Parse(proposed.AcceptedOperationalPolicySnapshot!) : null;
        var projection = TimeSpan.FromMinutes(command.CurrentProjectionMinutes ??
            accepted!.RootElement.GetProperty("Values").GetProperty("OCCUPANCY_VALIDITY_TIMESPAN").GetInt32());
        var facts = await repository.ReadAsync(proposed, command.EvaluatedAt, ct);
        var result = facts.EvaluateReservation(proposed, command.EvaluatedAt, projection, ct);
        if (!result.Allowed)
            throw new ReservationGateException(result.Indeterminate ? 503 : 409,
                result.Rejection!, "The requested capacity is unavailable or requires reconciliation.");
    }
}
