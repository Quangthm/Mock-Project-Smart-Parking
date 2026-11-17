using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Reservations;

internal static class AdmissionActor
{
    public static (Guid User, Guid Account) RequireDriver(ICurrentActor actor) =>
        actor.UserId is { } user && user != Guid.Empty && actor.AccountId is { } account && account != Guid.Empty && actor.Role == "driver"
            ? (user, account) : throw new ReservationGateException(403, "FORBIDDEN", "Driver access is required.");
}
