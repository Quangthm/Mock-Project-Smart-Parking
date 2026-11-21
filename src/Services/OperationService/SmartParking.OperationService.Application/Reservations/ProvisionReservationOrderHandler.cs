using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ProvisionReservationOrderHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IOperationsConcurrency guards, IUnitOfWork work,
    IReservationCommercialOwner commercial, IReservationFactsPublisher publisher, IReservationQuoteOwner quoteOwner)
    : IRequestHandler<ProvisionReservationOrderCommand>
{
    private static readonly TimeSpan AttemptLease = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    public async Task Handle(ProvisionReservationOrderCommand command, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(command.ReservationId, ct);
        if (observed?.RequestId is not { } workflowId) return;
        await using var lease = await workers.TryEnterAsync(workflowId, ct);
        if (lease is null) return;

        var retryState = "PENDING";
        DepositOrderRequest? orderRequest = null;
        await using (var transaction = await work.BeginTransactionAsync(ct))
        {
            await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
            await guards.LockSiteAsync(observed.SiteId, ct);
            var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
            if (booking is null || booking.DepositAmount is null or <= 0 ||
                booking.OrderProvisioningState is "CREATED" or "CLOSED_NO_ORDER" or "NOT_REQUIRED")
                return;

            var now = await bookings.DatabaseNowAsync(ct);
            await ReleaseExpiredHoldAsync(booking, now, ct);

            var legacy = booking.QuoteId is null ? BookingContracts.AcceptedTerms(booking) : null;
            var legacyExpired = legacy is not null && now >= legacy.ValidUntil;
            if (booking.OrderProvisioningState == "PENDING" &&
                (now >= booking.HoldExpiresAt || legacyExpired))
            {
                var code = now >= booking.HoldExpiresAt ? "PAYMENT_HOLD_EXPIRED" : "TERMS_CHANGED_OR_EXPIRED";
                booking.CloseOrderProvisioning(code);
                await ReleaseFailedOrderHoldAsync(booking, code, now, ct);
                await work.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }

            if (booking.OrderProvisioningState != "PENDING") retryState = "UNKNOWN";
            if (!booking.BeginOrderProvisioning(now, AttemptLease)) return;
            orderRequest = new DepositOrderRequest(booking.RequestId!.Value, booking.Id, booking.AccountId,
                booking.TenantId, booking.SiteId, booking.QuoteId ?? legacy!.QuoteId, booking.QuoteVersion ?? legacy!.Version,
                booking.DepositAmount.Value, booking.Currency!, booking.SelectedPaymentMethod!, booking.HoldExpiresAt!.Value);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        if (orderRequest is null) return;
        DepositOrderResult result;
        try
        {
            result = observed.QuoteId.HasValue ? await quoteOwner.EnsureOrderAsync(orderRequest, ct) :
                await commercial.EnsureOrderAsync(orderRequest, ct);
        }
        catch (ReservationGateException error) when (error.Code is "OWNER_UNAVAILABLE" or "PAYMENT_QUOTE_PROOF_UNAVAILABLE")
        {
            await RecordDeferredAsync(command.ReservationId, retryState, "PAYMENT_ORDER_PROVISIONING_UNAVAILABLE", ct);
            return;
        }
        catch (ReservationGateException error) when (error.Code == "PAYMENT_ORDER_OUTCOME_UNKNOWN")
        {
            await RecordDeferredAsync(command.ReservationId, "UNKNOWN", "PAYMENT_ORDER_OUTCOME_UNKNOWN", ct);
            return;
        }

        await RecordResultAsync(command.ReservationId, orderRequest, result, ct);
    }

    private async Task RecordResultAsync(Guid reservationId, DepositOrderRequest request,
        DepositOrderResult result, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(reservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared during order provisioning.");
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, reservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared during order provisioning.");
        var now = await bookings.DatabaseNowAsync(ct);
        await ReleaseExpiredHoldAsync(booking, now, ct);

        if (result.AcceptedRequest != request)
        {
            booking.DeferOrderProvisioning(now.Add(RetryDelay), "UNKNOWN", "PAYMENT_ORDER_RESPONSE_INVALID");
        }
        else if (result.State == "CREATED" && result.OrderId is { } orderId && orderId != Guid.Empty)
        {
            booking.RecordOrderCreated(orderId);
        }
        else if (result.State == "UNKNOWN" && result.OrderId is null)
        {
            booking.DeferOrderProvisioning(now.Add(RetryDelay), "UNKNOWN", "PAYMENT_ORDER_OUTCOME_UNKNOWN");
        }
        else if (result.State == "CLOSED_NO_ORDER" && result.OrderId is null &&
                 !string.IsNullOrWhiteSpace(result.OutcomeCode))
        {
            booking.CloseOrderProvisioning(result.OutcomeCode);
            if (booking.Status == "PENDING_PAYMENT")
                await ReleaseFailedOrderHoldAsync(booking, result.OutcomeCode, now, ct);
        }
        else
        {
            booking.DeferOrderProvisioning(now.Add(RetryDelay), "UNKNOWN", "PAYMENT_ORDER_RESPONSE_INVALID");
        }

        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task RecordDeferredAsync(Guid reservationId, string state, string code, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(reservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared during order provisioning.");
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, reservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared during order provisioning.");
        var now = await bookings.DatabaseNowAsync(ct);
        booking.DeferOrderProvisioning(now.Add(RetryDelay), state, code);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task ReleaseExpiredHoldAsync(ReservationRow booking, DateTimeOffset now, CancellationToken ct)
    {
        var previous = booking.Status;
        booking.ReleaseExpiredHold(now);
        if (previous == "PENDING_PAYMENT" && booking.Status == "EXPIRED")
            await PublishHoldAsync(booking, now, "EXPIRED", checked(booking.Version + 1), ct);
    }

    private Task ReleaseFailedOrderHoldAsync(ReservationRow booking, string code, DateTimeOffset now, CancellationToken ct)
    {
        if (booking.Status != "PENDING_PAYMENT") return Task.CompletedTask;
        booking.ReleaseHoldForOrderFailure(code);
        return PublishHoldAsync(booking, now, "EXPIRED", checked(booking.Version + 1), ct);
    }

    private Task PublishHoldAsync(ReservationRow booking, DateTimeOffset now, string state, int version, CancellationToken ct) =>
        publisher.HoldChangedAsync(new(Guid.NewGuid(), now, booking.RequestId!.Value, booking.Id,
            booking.AccountId, booking.TenantId, booking.SiteId, booking.TargetSlotId!.Value,
            booking.ExpectedStartTime, booking.ClaimEndTime ?? booking.ExpectedEndTime,
            booking.HoldExpiresAt!.Value, state, version), ct);

}
