using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ExpireReservationHoldHandler(IReservationBookingRepository bookings,
    IMediator mediator, IUnitOfWork work,
    IReservationFactsPublisher publisher)
    : IRequestHandler<ExpireReservationHoldCommand>
{
    public async Task Handle(ExpireReservationHoldCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var booking = await mediator.Send(new LockReservationBooking(command.ReservationId), ct);
        if (booking is null) return;
        var now = await bookings.DatabaseNowAsync(ct);
        if (booking.Status == "PENDING_PAYMENT" && booking.HoldExpiresAt <= now)
        {
            booking.ReleaseExpiredHold(now);
            await publisher.HoldChangedAsync(new(Guid.NewGuid(), now, booking.RequestId!.Value, booking.Id,
                booking.AccountId, booking.TenantId, booking.SiteId, booking.TargetSlotId!.Value,
                booking.ExpectedStartTime, booking.ClaimEndTime ?? booking.ExpectedEndTime,
                booking.HoldExpiresAt!.Value, "EXPIRED", checked(booking.Version + 1)), ct);
            await work.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
