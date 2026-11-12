using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ProvisionReservationOrderCommand(Guid ReservationId) : IRequest;
public sealed record ExpireReservationHoldCommand(Guid ReservationId) : IRequest;

public sealed class ProvisionReservationOrderHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IOperationsConcurrency guards, IUnitOfWork work,
    IReservationCommercialOwner commercial, IReservationFactsPublisher publisher)
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

            var quote = BookingContracts.AcceptedTerms(booking);
            if (booking.OrderProvisioningState == "PENDING" &&
                (now >= booking.HoldExpiresAt || now >= quote.ValidUntil))
            {
                var code = now >= booking.HoldExpiresAt ? "PAYMENT_HOLD_EXPIRED" : "TERMS_CHANGED_OR_EXPIRED";
                booking.CloseOrderProvisioning(code);
                await ReleaseFailedOrderHoldAsync(booking, code, now, ct);
                await work.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }

            if (!booking.BeginOrderProvisioning(now, AttemptLease)) return;
            orderRequest = new DepositOrderRequest(booking.RequestId!.Value, booking.Id, booking.AccountId,
                booking.TenantId, booking.SiteId, booking.VehicleId!.Value, booking.TargetSlotId!.Value,
                booking.ExpectedStartTime, booking.ExpectedEndTime, booking.TermsId!.Value,
                booking.TermsVersion!, quote, booking.HoldExpiresAt!.Value);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        if (orderRequest is null) return;
        DepositOrderResult result;
        try
        {
            result = await commercial.EnsureOrderAsync(orderRequest, ct);
        }
        catch (ReservationGateException error) when (error.Code == "OWNER_UNAVAILABLE")
        {
            await RecordDeferredAsync(command.ReservationId, "PENDING", "PAYMENT_ORDER_PROVISIONING_UNAVAILABLE", ct);
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

public sealed record AcceptZeroPriceTermsCommand(Guid ReservationId) : IRequest;

public sealed class AcceptZeroPriceTermsHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IOperationsConcurrency guards, IUnitOfWork work,
    IReservationCommercialOwner commercial, IReservationFactsPublisher publisher)
    : IRequestHandler<AcceptZeroPriceTermsCommand>
{
    private static readonly TimeSpan AttemptLease = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    public async Task Handle(AcceptZeroPriceTermsCommand command, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(command.ReservationId, ct);
        if (observed?.RequestId is not { } workflowId) return;
        await using var lease = await workers.TryEnterAsync(workflowId, ct);
        if (lease is null) return;

        ZeroPriceAcceptanceRequest? acceptanceRequest = null;
        await using (var transaction = await work.BeginTransactionAsync(ct))
        {
            await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
            await guards.LockSiteAsync(observed.SiteId, ct);
            var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
            if (booking is null || booking.DepositAmount != 0 ||
                booking.ZeroPriceAcceptanceState is "ACCEPTED" or "REJECTED" or "NOT_REQUIRED")
                return;

            var now = await bookings.DatabaseNowAsync(ct);
            var terms = BookingContracts.AcceptedTerms(booking);
            if (booking.ZeroPriceAcceptanceState == "PENDING" &&
                (now >= booking.HoldExpiresAt || now >= terms.ValidUntil))
            {
                var code = now >= booking.HoldExpiresAt ? "PAYMENT_HOLD_EXPIRED" : "TERMS_CHANGED_OR_EXPIRED";
                var wasPending = booking.Status == "PENDING_PAYMENT";
                booking.RejectZeroPriceTerms(code);
                if (wasPending) await PublishHoldAsync(booking, now, "EXPIRED", checked(booking.Version + 1), ct);
                await work.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }

            if (!booking.BeginZeroPriceAcceptance(now, AttemptLease)) return;
            acceptanceRequest = new ZeroPriceAcceptanceRequest(booking.RequestId!.Value, booking.Id,
                booking.AccountId, booking.TenantId, booking.SiteId, booking.VehicleId!.Value,
                booking.TargetSlotId!.Value, booking.ExpectedStartTime, booking.ExpectedEndTime,
                booking.TermsId!.Value, booking.TermsVersion!, terms, booking.HoldExpiresAt!.Value);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        if (acceptanceRequest is null) return;
        ZeroPriceAcceptanceResult result;
        try
        {
            result = await commercial.AcceptZeroPriceAsync(acceptanceRequest, ct);
        }
        catch (ReservationGateException error) when (error.Code == "OWNER_UNAVAILABLE")
        {
            await RecordDeferredAsync(command.ReservationId, "PENDING", "ZERO_PRICE_ACCEPTANCE_UNAVAILABLE", ct);
            return;
        }
        catch (ReservationGateException error) when (error.Code == "ZERO_PRICE_ACCEPTANCE_OUTCOME_UNKNOWN")
        {
            await RecordDeferredAsync(command.ReservationId, "UNKNOWN", "ZERO_PRICE_ACCEPTANCE_OUTCOME_UNKNOWN", ct);
            return;
        }

        var observedResult = await bookings.FindOrderProvisioningAsync(command.ReservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared during zero-price acceptance.");
        await using var resultTransaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(observedResult.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observedResult.SiteId, ct);
        var locked = await bookings.FindForUpdateAsync(observedResult.TenantId, observedResult.SiteId, observedResult.Id, ct)
            ?? throw new InvalidOperationException("Reservation disappeared during zero-price acceptance.");
        var evaluatedNow = await bookings.DatabaseNowAsync(ct);
        if (result.AcceptedRequest != acceptanceRequest)
        {
            locked.DeferZeroPriceAcceptance(evaluatedNow.Add(RetryDelay), "UNKNOWN", "ZERO_PRICE_ACCEPTANCE_RESPONSE_INVALID");
        }
        else if (result.State == "ACCEPTED")
        {
            locked.AcceptZeroPriceTerms(evaluatedNow);
            await ReleaseExpiredHoldAsync(locked, evaluatedNow, ct);
        }
        else if (result.State == "UNKNOWN")
        {
            locked.DeferZeroPriceAcceptance(evaluatedNow.Add(RetryDelay), "UNKNOWN", "ZERO_PRICE_ACCEPTANCE_OUTCOME_UNKNOWN");
        }
        else if (result.State == "REJECTED" && !string.IsNullOrWhiteSpace(result.OutcomeCode))
        {
            var wasPending = locked.Status == "PENDING_PAYMENT";
            locked.RejectZeroPriceTerms(result.OutcomeCode);
            if (wasPending) await PublishHoldAsync(locked, evaluatedNow, "EXPIRED", checked(locked.Version + 1), ct);
        }
        else
        {
            locked.DeferZeroPriceAcceptance(evaluatedNow.Add(RetryDelay), "UNKNOWN", "ZERO_PRICE_ACCEPTANCE_RESPONSE_INVALID");
        }

        await work.SaveChangesAsync(ct);
        await resultTransaction.CommitAsync(ct);
    }

    private async Task RecordDeferredAsync(Guid reservationId, string state, string code, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(reservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared during zero-price acceptance.");
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, reservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared during zero-price acceptance.");
        var now = await bookings.DatabaseNowAsync(ct);
        booking.DeferZeroPriceAcceptance(now.Add(RetryDelay), state, code);
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

    private Task PublishHoldAsync(ReservationRow booking, DateTimeOffset now, string state, int version, CancellationToken ct) =>
        publisher.HoldChangedAsync(new(Guid.NewGuid(), now, booking.RequestId!.Value, booking.Id,
            booking.AccountId, booking.TenantId, booking.SiteId, booking.TargetSlotId!.Value,
            booking.ExpectedStartTime, booking.ClaimEndTime ?? booking.ExpectedEndTime,
            booking.HoldExpiresAt!.Value, state, version), ct);
}

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
