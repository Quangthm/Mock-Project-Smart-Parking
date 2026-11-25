using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ConfirmReservationHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IReservationAvailabilityRepository availability,
    IReservationFacilityDependencyRepository dependencies, IOperationsConcurrency guards,
    IUnitOfWork work, IReservationFactsPublisher publisher, IMediator mediator,
    IReservationCommercialOwner commercial, IReservationQuoteOwner quoteOwner)
    : IRequestHandler<ConfirmPaidReservationCommand>, IRequestHandler<ConfirmZeroPriceReservationCommand>
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public Task Handle(ConfirmPaidReservationCommand command, CancellationToken ct) =>
        ConfirmAsync(command.ReservationId, command.CollectionId, ct);

    public Task Handle(ConfirmZeroPriceReservationCommand command, CancellationToken ct) =>
        ConfirmAsync(command.ReservationId, null, ct);

    private async Task ConfirmAsync(Guid reservationId, Guid? collectionId, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(reservationId, ct);
        if (observed?.RequestId is not { } workflowId) return;
        await using var lease = await workers.TryEnterAsync(workflowId, ct);
        if (lease is null) return;

        if (observed.ConfirmationState == "RECONCILIATION_REQUIRED")
        {
            await ReconcileAsync(observed, ct);
            return;
        }

        await using var transaction = await work.BeginTransactionAsync(ct);
        await availability.LockAccountAsync(observed.AccountId, ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
        if (booking?.ConfirmationState != "PENDING" || booking.CollectionId != collectionId ||
            (booking.DepositAmount > 0) != collectionId.HasValue) return;

        var now = await bookings.DatabaseNowAsync(ct);
        await ReleaseExpiredHoldAsync(booking, now, ct);
        var deadline = collectionId.HasValue ? booking.ExpectedStartTime : booking.HoldExpiresAt!.Value;
        if (booking.Status is not ("PENDING_PAYMENT" or "EXPIRED"))
        {
            await DeclineAsync(booking, "RESERVATION_NOT_ELIGIBLE", now, ct);
            await transaction.CommitAsync(ct);
            return;
        }
        if (now >= deadline || !collectionId.HasValue && !HasClaim(booking))
        {
            await DeclineAsync(booking, now >= booking.ExpectedStartTime ? "BOOKING_START_REACHED" : "PAYMENT_HOLD_EXPIRED", now, ct);
            await transaction.CommitAsync(ct);
            return;
        }
        var fingerprint = booking.QuoteId is null ? BookingContracts.Fingerprint(BookingContracts.Selection(booking)) :
            FacilityDependencyProtocol.Fingerprint(JsonSerializer.Serialize(new { booking.RequestId, booking.QuoteId, booking.QuoteVersion }));
        var dependency = await dependencies.FindByWorkflowPurposeAsync(workflowId, "CONFIRMATION", ct);
        if (dependency is null)
        {
            dependencies.Add(ReservationFacilityDependency.Prepare(Guid.NewGuid(), workflowId, "CONFIRMATION",
                booking.TenantId, booking.SiteId, booking.TargetSlotId!.Value, fingerprint,
                deadline, now));
            booking.DeferConfirmation(now.Add(RetryDelay), "FACILITY_ACCEPTANCE_PENDING");
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        dependency = await dependencies.LockAsync(dependency.Id, ct)
            ?? throw new InvalidOperationException("Confirmation facility dependency disappeared.");
        if (dependency.State != "HELD" || now >= dependency.Deadline)
        {
            booking.DeferConfirmation(now.Add(RetryDelay), "FACILITY_ACCEPTANCE_PENDING");
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        var resourceFacts = JsonSerializer.Deserialize<FacilityResourceSnapshot>(dependency.SnapshotJson!)
            ?? throw new InvalidOperationException("Stored Facility facts are invalid.");

        await bookings.RequireDurableConfirmationAsync(ct);
        var evaluatedAt = await bookings.DatabaseNowAsync(ct);
        await ReleaseExpiredHoldAsync(booking, evaluatedAt, ct);
        if (evaluatedAt >= deadline || evaluatedAt >= dependency.Deadline || !collectionId.HasValue && !HasClaim(booking))
        {
            dependency.BeginAbort("CONFIRMATION_DEADLINE_REACHED", evaluatedAt);
            await DeclineAsync(booking, evaluatedAt >= booking.ExpectedStartTime ? "BOOKING_START_REACHED" : "PAYMENT_HOLD_EXPIRED", evaluatedAt, ct);
            await transaction.CommitAsync(ct);
            return;
        }

        try
        {
            // Expired paid holds must compete again under the same account, plate, site and Facility guards.
            EffectiveReservationPolicy? reacquisitionPolicy = null;
            if (!HasClaim(booking) && collectionId.HasValue)
            {
                reacquisitionPolicy = await mediator.Send(new ResolveReservationPoliciesQuery(booking.TenantId, booking.SiteId, evaluatedAt), ct);
                var interval = new ReservationInterval(booking.ExpectedStartTime, booking.ExpectedEndTime);
                var requiredGap = TimeSpan.FromMinutes(reacquisitionPolicy.Values["INTER_BOOKING_BUFFER"]);
                if (!reacquisitionPolicy.AllowsWindow(interval, evaluatedAt) ||
                    (booking.ClaimEndTime ?? interval.End) - interval.End < requiredGap)
                    throw new ReservationGateException(409, "BOOKING_POLICY_CHANGED", "The original claim cannot satisfy current policy.");
                await mediator.Send(new RequireBookingPermissionCommand(booking.AccountId, reacquisitionPolicy, evaluatedAt), ct);
                await ReleaseForUnavailableConfirmationAsync(booking, "CLAIM_REACQUISITION", evaluatedAt, ct);
            }
            await bookings.RequirePlateAvailableAsync(booking, evaluatedAt, ct);
            RequireSelectedSlotEvidence.Validate(booking, resourceFacts);
            await mediator.Send(new RequireReservationAvailabilityCommand(booking, evaluatedAt, reacquisitionPolicy), ct);
        }
        catch (ReservationGateException error) when (error.StatusCode == 409)
        {
            dependency.BeginAbort(error.Code, evaluatedAt);
            await DeclineAsync(booking, error.Code, evaluatedAt, ct);
            await transaction.CommitAsync(ct);
            return;
        }
        catch (ReservationGateException error) when (error.StatusCode == 503)
        {
            booking.DeferConfirmation(evaluatedAt.Add(RetryDelay), error.Code);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        if (booking.Status == "EXPIRED") booking.ConfirmReacquiredPaidClaim(collectionId!.Value, evaluatedAt);
        else booking.ConfirmAtEvaluation(collectionId, evaluatedAt);
        dependency.MarkReady(JsonSerializer.Serialize(new
        {
            reservationId = booking.Id,
            outcome = "CONFIRMED",
            evaluatedAt,
            confirmationEventId = booking.ConfirmationEffectId
        }), evaluatedAt);
        await publisher.ConfirmedAsync(new(booking.ConfirmationEffectId!.Value, booking.Id, booking.AccountId,
            booking.TenantId, booking.SiteId, evaluatedAt), ct);
        await publisher.AvailabilityChangedAsync(new(Guid.NewGuid(), evaluatedAt, booking.TenantId,
            booking.SiteId, booking.TargetSlotId!.Value, booking.ExpectedStartTime,
            booking.ClaimEndTime ?? booking.ExpectedEndTime, "CONFIRMED"), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static bool HasClaim(ReservationRow row) => row.Status == "PENDING_PAYMENT" &&
        row.ActiveClaimSlotId.HasValue && row.ActiveClaimSlotId == row.TargetSlotId;

    private async Task DeclineAsync(ReservationRow booking, string reason, DateTimeOffset now, CancellationToken ct)
    {
        await ReleaseForUnavailableConfirmationAsync(booking, reason, now, ct);
        if (booking.CollectionId.HasValue || booking.ZeroReceiptId.HasValue)
            booking.RequireReconciliation(reason, now);
        else booking.CloseConfirmation(reason);
        await work.SaveChangesAsync(ct);
    }

    private async Task ReconcileAsync(ReservationRow booking, CancellationToken ct)
    {
        var acknowledged = false;
        try
        {
            await NotifyReconciliationAsync(booking, ct);
            acknowledged = true;
        }
        catch (ReservationGateException failure) when (failure.StatusCode == 503)
        {
            // Unknown owner outcome. Retry the same financial identity on the existing due queue.
        }
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(booking.NormalizedPlate!, ct);
        await guards.LockSiteAsync(booking.SiteId, ct);
        var current = await bookings.FindForUpdateAsync(booking.TenantId, booking.SiteId, booking.Id, ct);
        if (current?.ConfirmationState != "RECONCILIATION_REQUIRED") return;
        if (acknowledged) current.ReconciliationAccepted();
        else current.ScheduleReconciliation((await bookings.DatabaseNowAsync(ct)).Add(RetryDelay));
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task NotifyReconciliationAsync(ReservationRow booking, CancellationToken ct)
    {
        if (booking.CollectionId is Guid collectionId)
        {
            var legacy = booking.QuoteId.HasValue ? null : BookingContracts.AcceptedTerms(booking);
            var collection = new VerifiedCollection(collectionId, booking.PaymentOrderId!.Value, booking.Id,
                booking.AccountId, booking.TenantId, booking.SiteId, booking.QuoteId ?? legacy!.QuoteId,
                booking.QuoteVersion ?? legacy!.Version, booking.DepositAmount!.Value, booking.Currency!);
            await commercial.ReconcileAsync(booking.Id, collection, booking.ConfirmationCode!, ct);
        }
        else
        {
            var evidence = new ZeroOrderEvidence(BookingContracts.ZeroBinding(booking), booking.PaymentOrderId!.Value, booking.ZeroReceiptId!.Value);
            await quoteOwner.ReconcileZeroAsync(evidence, booking.ConfirmationCode!, ct);
        }
    }

    private async Task ReleaseExpiredHoldAsync(ReservationRow booking, DateTimeOffset now, CancellationToken ct)
    {
        var previous = booking.Status;
        booking.ReleaseExpiredHold(now);
        if (previous == "PENDING_PAYMENT" && booking.Status == "EXPIRED")
            await PublishHoldAsync(booking, now, "EXPIRED", checked(booking.Version + 1), ct);
    }

    private async Task ReleaseForUnavailableConfirmationAsync(ReservationRow booking, string code,
        DateTimeOffset now, CancellationToken ct)
    {
        if (booking.Status != "PENDING_PAYMENT") return;
        booking.ReleaseHoldForUnavailableConfirmation(code);
        await PublishHoldAsync(booking, now, "EXPIRED", checked(booking.Version + 1), ct);
    }

    private Task PublishHoldAsync(ReservationRow booking, DateTimeOffset now, string state, int version, CancellationToken ct) =>
        publisher.HoldChangedAsync(new(Guid.NewGuid(), now, booking.RequestId!.Value, booking.Id,
            booking.AccountId, booking.TenantId, booking.SiteId, booking.TargetSlotId!.Value,
            booking.ExpectedStartTime, booking.ClaimEndTime ?? booking.ExpectedEndTime,
            booking.HoldExpiresAt!.Value, state, version), ct);
}
