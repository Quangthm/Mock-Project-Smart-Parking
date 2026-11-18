using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

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
                terms, booking.HoldExpiresAt!.Value);
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
