using MediatR;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
namespace SmartParking.OperationService.Application.Reservations;

public sealed class LockReservationBookingHandler(IReservationBookingRepository bookings,
    IReservationAvailabilityRepository availability, IOperationsConcurrency guards) : IRequestHandler<LockReservationBooking, ReservationRow?>
{
    public async Task<ReservationRow?> Handle(LockReservationBooking command, CancellationToken ct)
    {
        var observed = await bookings.FindBookingAsync(command.ReservationId, ct);
        if (observed?.RequestId is null) return null;
        await availability.LockAccountAsync(observed.AccountId, ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        return await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
    }
}
