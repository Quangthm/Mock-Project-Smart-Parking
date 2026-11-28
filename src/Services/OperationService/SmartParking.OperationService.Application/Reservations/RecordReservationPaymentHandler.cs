using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
namespace SmartParking.OperationService.Application.Reservations;

public sealed class RecordReservationPaymentHandler(IReservationBookingRepository bookings, IMediator mediator,
    IUnitOfWork work, IStructureRepository structure, IReservationPolicyRepository policies, IReservationFactsPublisher facts,
    ILogger<RecordReservationPaymentHandler> logger)
    : IRequestHandler<RecordReservationPaymentCommand>
{
    public async Task Handle(RecordReservationPaymentCommand command, CancellationToken ct)
    {
        var e = command.Evidence;
        if (e.EventId == Guid.Empty || e.ReservationId == Guid.Empty || e.AccountId == Guid.Empty ||
            e.SiteId == Guid.Empty || e.TenantId == Guid.Empty || e.OccurredAt == default)
            throw new InvalidOperationException("Incomplete reservation settlement.");
        await using var tx = await work.BeginTransactionAsync(ct);
        var row = await mediator.Send(new LockReservationBooking(e.ReservationId), ct);
        if (row?.QuoteId is null) return; // Shared publication also carries unrelated historical references.
        if (row.AccountId != e.AccountId || row.SiteId != e.SiteId || row.TenantId != e.TenantId ||
            row.DepositAmount != e.Amount || row.Currency != e.Currency || row.SelectedPaymentMethod != e.PaymentMethod ||
            e.PlateNumber != null && ReservationCarPlate.Canonicalize(e.PlateNumber) != row.NormalizedPlate)
            throw new InvalidOperationException("Settlement conflicts with the accepted reservation.");
        var fingerprint = FormattableString.Invariant($"{e.PaymentTransactionId:N}/{e.PaymentOrderId:N}/{e.OccurredAt.UtcTicks}/{e.Amount:G29}/{e.Currency}/{e.PaymentMethod}");
        if (!row.BindCollection(e.PaymentOrderId, e.PaymentTransactionId, fingerprint))
        {
            logger.LogInformation("Reservation settlement replay. EventId {EventId}, ReservationId {ReservationId}", e.EventId, e.ReservationId);
            return;
        }
        await bookings.RequireDurableConfirmationAsync(ct);
        var at = await bookings.DatabaseNowAsync(ct);
        var denial = await DenialAsync(row, at, ct);
        var timely = row.Status == "PENDING_PAYMENT" && at < row.HoldExpiresAt;
        if (denial is null)
        {
            row.ConfirmPaid(at);
            await facts.ConfirmedAsync(new(row.ConfirmationEffectId!.Value, row.Id, row.AccountId, row.TenantId, row.SiteId, at), ct);
        }
        else
        {
            row.DenyPaidEntitlement(denial);
            await facts.RequestDispositionAsync(new(row.Id, row.AccountId, row.TenantId, row.SiteId, row.QuoteId.Value,
                e.PaymentOrderId, e.PaymentTransactionId, null, e.Amount, e.Currency, denial), ct);
        }
        await facts.AvailabilityChangedAsync(new(Guid.NewGuid(), at, row.TenantId, row.SiteId, row.TargetSlotId!.Value,
            row.ExpectedStartTime, row.ClaimEndTime!.Value, row.Status), ct);
        await work.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var outcome = denial is not null ? "DENIED" : timely ? "TIMELY_CONFIRMATION" : "LATE_REACQUISITION";
        logger.LogInformation("Reservation entitlement outcome recorded. EventId {EventId}, ReservationId {ReservationId}, outcome {Outcome}, reason {Reason}",
            e.EventId, e.ReservationId, outcome, denial);
    }

    private async Task<string?> DenialAsync(ReservationRow row, DateTimeOffset at, CancellationToken ct)
    {
        if (row.Status is "CANCELLED" or "INVALIDATED" or "UNFULFILLABLE") return row.Status;
        if (row.Status is not ("EXPIRED" or "PENDING_PAYMENT") || row.HoldExpiresAt is null)
            throw new InvalidOperationException("Inconsistent paid reservation state.");
        if (at >= row.ExpectedStartTime) return "RESERVATION_START_PASSED";
        if (await structure.HoldAsync(row.SiteId, ct) != null)
            throw new InvalidOperationException("Site structure edit in progress.");
        var site = await structure.StateAsync(row.SiteId, ct);
        if (site != null && site.TenantId != row.TenantId) throw new InvalidOperationException("Contradictory site scope.");
        if (site is { Active: false }) return "SITE_INACTIVE";
        if (row.Status == "PENDING_PAYMENT" && at < row.HoldExpiresAt)
        {
            if (row.TargetSlotId == null || row.ActiveClaimSlotId != row.TargetSlotId)
                throw new InvalidOperationException("Protected hold lost its original slot.");
            return null;
        }
        var revisions = await policies.ReadEffectiveAsync(row.TenantId, row.SiteId, at, ct);
        var platform = revisions.SingleOrDefault(p => p.TenantId == null && p.SiteId == null);
        var local = revisions.SingleOrDefault(p => p.TenantId == row.TenantId && p.SiteId == row.SiteId);
        const string key = "OCCUPANCY_VALIDITY_TIMESPAN";
        if (platform == null || !platform.Values.TryGetValue(key, out var projection) || projection < 0)
            throw new ReservationPolicyUnavailableException("Current occupancy projection unavailable.");
        if (local != null && local.Values.TryGetValue(key, out var minutes) && minutes != projection)
        {
            if (minutes < 0 || !platform.OverrideBounds.TryGetValue(key, out var bound) ||
                minutes < bound.Minimum || minutes > bound.Maximum)
                throw new ReservationPolicyUnavailableException("Occupancy projection outside approved bounds.");
            projection = minutes;
        }
        try
        {
            await bookings.RequirePlateAvailableAsync(row, at, ct);
            await mediator.Send(new RequireReservationAvailabilityCommand(row, at, projection), ct);
        }
        catch (ReservationGateException error) when (error.StatusCode == 409) { return error.Code; }
        return null;
    }
}
