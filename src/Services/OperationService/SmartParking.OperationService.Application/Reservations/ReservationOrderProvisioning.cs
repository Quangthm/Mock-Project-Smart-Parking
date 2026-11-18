using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ProvisionReservationOrderCommand(Guid ReservationId) : IRequest;
public sealed record ExpireReservationHoldCommand(Guid ReservationId) : IRequest;
public sealed record AcceptZeroPriceTermsCommand(Guid ReservationId) : IRequest;
public sealed class ExpireReservationHoldHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IOperationsConcurrency guards, IUnitOfWork work,
    IReservationFactsPublisher publisher)
    : IRequestHandler<ExpireReservationHoldCommand>
{
    public async Task Handle(ExpireReservationHoldCommand command, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(command.ReservationId, ct);
        if (observed?.RequestId is not { } workflowId) return;
        await using var lease = await workers.TryEnterAsync(workflowId, ct);
        if (lease is null) return;

        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
        if (booking is null) return;
        var now = await bookings.DatabaseNowAsync(ct);
        if (booking.Status == "PENDING_PAYMENT" && booking.HoldExpiresAt <= now)
        {
            booking.ReleaseExpiredHold(now);
            if (booking.OrderProvisioningState == "PENDING")
                booking.CloseOrderProvisioning("PAYMENT_HOLD_EXPIRED");
            await publisher.HoldChangedAsync(new(Guid.NewGuid(), now, booking.RequestId!.Value, booking.Id,
                booking.AccountId, booking.TenantId, booking.SiteId, booking.TargetSlotId!.Value,
                booking.ExpectedStartTime, booking.ClaimEndTime ?? booking.ExpectedEndTime,
                booking.HoldExpiresAt!.Value, "EXPIRED", checked(booking.Version + 1)), ct);
            await work.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
