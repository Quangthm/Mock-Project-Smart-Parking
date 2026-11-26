using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ExpireReservationHoldHandler(IReservationBookingRepository bookings,
    IMediator mediator, IUnitOfWork work,
    IReservationFactsPublisher publisher)
    : IRequestHandler<ExpireReservationHoldCommand, ReservationDeadlineResult>
{
    public async Task<ReservationDeadlineResult> Handle(ExpireReservationHoldCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var booking = await mediator.Send(new LockReservationBooking(command.ReservationId), ct);
        if (booking is not { Status: "PENDING_PAYMENT", QuoteId: not null, RequestId: not null, DepositAmount: > 0 } ||
            booking.HoldExpiresAt != command.ExpectedAt) return ReservationDeadlineResult.Obsolete;
        var now = await bookings.DatabaseNowAsync(ct);
        if (now < command.ExpectedAt) return ReservationDeadlineResult.Early(command.ExpectedAt - now);
        booking.ReleaseExpiredHold(now);
        await publisher.HoldChangedAsync(new(Guid.NewGuid(), now, booking.RequestId!.Value, booking.Id,
            booking.AccountId, booking.TenantId, booking.SiteId, booking.TargetSlotId!.Value,
            booking.ExpectedStartTime, booking.ClaimEndTime ?? booking.ExpectedEndTime,
            booking.HoldExpiresAt!.Value, "EXPIRED", checked(booking.Version + 1)), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ReservationDeadlineResult.Completed;
    }
}
