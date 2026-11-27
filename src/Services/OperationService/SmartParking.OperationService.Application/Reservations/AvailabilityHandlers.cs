using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class RequireBookingPermissionHandler(IReservationAvailabilityRepository repository)
    : IRequestHandler<RequireBookingPermissionCommand>
{
    public async Task Handle(RequireBookingPermissionCommand command, CancellationToken ct)
    {
        // This account guard precedes plate and site guards. Existing rights are not reauthorized.
        await repository.LockAccountAsync(command.AccountId, ct);
        if (await repository.HasUnclassifiedCancellationAsync(command.AccountId, ct))
            throw new ReservationGateException(503, "CANCELLATION_HISTORY_RECONCILIATION_REQUIRED",
                "Cancellation history must be reconciled before a new booking can be accepted.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(command.Policy.CancellationTimezone);
        var local = TimeZoneInfo.ConvertTime(command.EvaluatedAt, zone);
        var midnight = new DateTimeOffset(local.Date, local.Offset);
        var evidence = await repository.ReadCancellationsAsync(command.AccountId, midnight.ToUniversalTime(), command.EvaluatedAt, ct);
        if (evidence.Any(e => e.RestrictedUntil > command.EvaluatedAt) ||
            evidence.Count(e => e.Reason == "VOLUNTARY" && e.CancelledAt >= midnight && e.CancelledAt <= command.EvaluatedAt)
                >= command.Policy.Values["DAILY_CANCELLATION_LIMIT"])
            throw new ReservationGateException(409, "BOOKING_RESTRICTED", "New bookings are temporarily restricted.");
    }
}
