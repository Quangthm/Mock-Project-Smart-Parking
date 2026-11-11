using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ProgressReservationHandler(IReservationRequestRepository requests,
    IReservationTermsRepository reviews, IReservationBookingRepository bookings,
    IReservationVehicleOwner vehicles, IReservationCommercialOwner commercial,
    IReservationFacilityDependencyRepository dependencies, IReservationWorkflowConcurrency workers,
    IReservationAvailabilityRepository availability, IReservationFactsPublisher publisher,
    IOperationsConcurrency guards, IUnitOfWork work, IMediator mediator)
    : IRequestHandler<ProgressReservationCommand>
{
    public async Task Handle(ProgressReservationCommand command, CancellationToken ct)
    {
        await using var lease = await workers.TryEnterAsync(command.RequestId, ct);
        if (lease is null) return;
        var observed = await requests.FindForWorkerAsync(command.RequestId, ct);
        if (observed is null || observed.State is not ("PREPARING" or "BOOKING")) return;
        if (observed.State == "PREPARING" && observed.VehicleBindingState is null)
        {
            await RecordBindingAsync(observed, ct);
            return;
        }
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request is null || request.State is not ("PREPARING" or "BOOKING")) return;
        var now = await bookings.DatabaseNowAsync(ct);
        try
        {
            if (request.State == "PREPARING") await PrepareAsync(request, now, ct);
            else await ProgressBookingAsync(request, now, ct);
        }
        catch (ReservationGateException error) when (error.Code is "OWNER_UNAVAILABLE" or
            "CAPACITY_COORDINATION_UNAVAILABLE" or "OWNER_CONTRACT_INVALID")
        {
            // Preserve durable binding/booking progress; unknown remote effects are not failures.
            request.Defer(now.AddSeconds(1), error.Code);
        }
        catch (ReservationGateException error) when (request.State == "PREPARING" && error.StatusCode is 400 or 409)
        { request.Reject(error.Code); }
        if (request.State is "REJECTED" or "EXPIRED")
            await AbortUnusedDependenciesAsync(request.Id, await bookings.DatabaseNowAsync(ct), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task RecordBindingAsync(ReservationRequest observed, CancellationToken ct)
    {
        var input = JsonSerializer.Deserialize<CreateReservationRequest>(observed.IntentJson)!;
        var now = await bookings.DatabaseNowAsync(ct);
        VehicleBindingResult? result = null;
        // The workflow session guard prevents concurrent worker evaluations, while no database
        // transaction, plate guard or site guard is held during the owner call.
        if (now < observed.OverallDeadline && now < input.Start)
            result = await vehicles.ResolveAsync(observed.AccountId, input.VehicleId, ct);
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(observed.Id, ct);
        if (request is null || request.State != "PREPARING" || request.VehicleBindingState is not null) return;
        now = await bookings.DatabaseNowAsync(ct);
        if (result is null || now >= request.OverallDeadline || now >= input.Start)
            request.Reject("BOOKING_PREPARATION_EXPIRED");
        else request.RecordVehicleBinding(result.Eligible, result.Revision, result.CanonicalPlate, now);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task PrepareAsync(ReservationRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var input = JsonSerializer.Deserialize<CreateReservationRequest>(request.IntentJson)
            ?? throw new InvalidOperationException("Stored booking intent is invalid.");
        if (now >= input.Start || now >= request.OverallDeadline)
        { request.Reject("BOOKING_PREPARATION_EXPIRED"); return; }
        if (request.VehicleBindingState != "ACTIVE") return;
        var selection = BookingContracts.Selection(input);
        var review = await reviews.LockAsync(input.TermsId, ct);
        if (review is null || !review.CanAccept(request.AccountId, BookingContracts.Fingerprint(selection),
            input.TermsVersion, request.Id, now))
        { request.Reject("TERMS_CHANGED_OR_EXPIRED"); return; }
        var accepted = JsonSerializer.Deserialize<BookingTermsSnapshot>(review.SnapshotJson)
            ?? throw new InvalidOperationException("Stored reviewed terms are invalid.");
        var dependency = await RequireDependencyAsync(request, "ADMISSION", accepted.Facility.TenantId,
            input.SiteId, input.RequestedSlotId, request.OverallDeadline < input.Start ? request.OverallDeadline : input.Start, now, ct);
        if (dependency is null) return;
        var resourceFacts = JsonSerializer.Deserialize<FacilityResourceSnapshot>(dependency.SnapshotJson!)
            ?? throw new InvalidOperationException("Stored Facility facts are invalid.");
        await availability.LockAccountAsync(request.AccountId, ct);
        await guards.LockVehicleAsync(request.VehiclePlate!, ct);
        await guards.LockSiteAsync(input.SiteId, ct);
        var current = resourceFacts.Booking;
        BookingContracts.ValidateFacility(current, selection);
        var policy = await mediator.Send(new ResolveReservationPoliciesQuery(current.TenantId, current.SiteId, now), ct);
        if (current != accepted.Facility || policy.Platform.Id != accepted.PlatformPolicyId ||
            policy.Platform.Version != accepted.PlatformPolicyVersion || policy.Site.Id != accepted.SitePolicyId ||
            policy.Site.Version != accepted.SitePolicyVersion)
        { request.Reject("TERMS_CHANGED_OR_EXPIRED"); return; }
        now = await bookings.DatabaseNowAsync(ct);
        await mediator.Send(new RequireBookingPermissionCommand(request.AccountId, policy, now), ct);
        if (now >= request.OverallDeadline || !review.CanAccept(request.AccountId, BookingContracts.Fingerprint(selection),
            input.TermsVersion, request.Id, now) || !policy.AllowsWindow(new(input.Start, input.End), now))
        { request.Reject("TERMS_CHANGED_OR_EXPIRED"); return; }
        BookingContracts.ValidateQuote(accepted.Commercial, request.AccountId, selection, now);
        var booking = ReservationRow.Prepare(request.Id, request.Id, request.AccountId, input.VehicleId,
            current.TenantId, current.SiteId, input.RequestedSlotId, review.Id, review.Version, input.PaymentMethod,
            input.Start, input.End, input.End.AddMinutes(policy.Values["INTER_BOOKING_BUFFER"]), now,
            policy.DeadlinesFor(new(input.Start, input.End), now).PaymentDueAt,
            accepted.Commercial.BookedAmount, accepted.Commercial.DepositAmount, accepted.Commercial.Currency,
            accepted.PolicyJson, JsonSerializer.Serialize(accepted.Commercial));
        booking.OriginalPlate = request.VehiclePlate;
        booking.NormalizedPlate = request.VehiclePlate;
        await bookings.RequirePlateAvailableAsync(booking, now, ct);
        await mediator.Send(new RequireReservationAvailabilityCommand(booking, resourceFacts, now), ct);
        if (!await commercial.AcceptAsync(request.Id, accepted.Commercial, ct))
        { request.Reject("TERMS_CHANGED_OR_EXPIRED"); return; }
        // The owner accepts by stable workflow ID. A lost local commit replays that same acceptance.
        now = await bookings.DatabaseNowAsync(ct);
        if (now >= booking.HoldExpiresAt || now >= request.OverallDeadline)
        { request.Reject("BOOKING_PREPARATION_EXPIRED"); return; }
        review.Accept(request.Id);
        await bookings.AddAsync(booking, ct);
        request.AttachBooking(booking.Id);
        await PublishHoldAsync(booking, now, "PENDING_PAYMENT", booking.Version, ct);
        dependency.MarkReady(JsonSerializer.Serialize(new { reservationId = booking.Id, outcome = "HOLD_COMMITTED" }), now);
    }

    private async Task ProgressBookingAsync(ReservationRequest request, DateTimeOffset now, CancellationToken ct)
    {
        var input = JsonSerializer.Deserialize<CreateReservationRequest>(request.IntentJson)!;
        await guards.LockVehicleAsync(request.VehiclePlate!, ct);
        await guards.LockSiteAsync(input.SiteId, ct);
        var own = await bookings.FindOwnBookingAsync(request.AccountId, request.ReservationId!.Value, ct)
            ?? throw new InvalidOperationException("Workflow booking is missing.");
        var booking = await bookings.FindForUpdateAsync(own.TenantId, own.SiteId, own.Id, ct)
            ?? throw new InvalidOperationException("Workflow booking is missing.");
        now = await bookings.DatabaseNowAsync(ct);
        await ReleaseExpiredHoldAsync(booking, now, ct);
        var quote = JsonSerializer.Deserialize<CommercialTerms>(booking.AcceptedPricingSnapshot!)!;
        if (booking.DepositAmount == 0)
        {
            if (now >= booking.ExpectedStartTime) { request.FinishBooking("EXPIRED", "BOOKING_START_REACHED"); return; }
            await ConfirmAsync(request, booking, null, ct);
            return;
        }
        var orderRequest = new DepositOrderRequest(request.Id, booking.Id, booking.AccountId, booking.TenantId,
            booking.SiteId, quote.QuoteId, booking.TermsVersion!, booking.DepositAmount!.Value, booking.Currency!,
            booking.SelectedPaymentMethod!, booking.HoldExpiresAt!.Value);
        if (booking.PaymentOrderId is null)
        {
            // Producer MUST replay by operation ID, and MUST NOT create a new order after H.
            var order = await commercial.EnsureOrderAsync(orderRequest, ct);
            if (order.AcceptedRequest != orderRequest) throw BookingContracts.InvalidOwner();
            if (order.State == "UNKNOWN" && order.OrderId is null)
            { request.Defer(now.AddSeconds(1), "PAYMENT_ORDER_OUTCOME_UNKNOWN"); return; }
            if (order.State == "CLOSED_NO_ORDER" && order.OrderId is null)
            {
                now = await bookings.DatabaseNowAsync(ct);
                if (now < booking.HoldExpiresAt) throw BookingContracts.InvalidOwner();
                await ReleaseExpiredHoldAsync(booking, now, ct);
                request.FinishBooking("EXPIRED", "PAYMENT_HOLD_EXPIRED");
                return;
            }
            if (order.State != "CREATED" || order.OrderId is null || order.OrderId == Guid.Empty)
                throw BookingContracts.InvalidOwner();
            booking.RecordOrder(order.OrderId.Value);
            return;
        }
        var lookup = await commercial.CollectionsAsync(booking.Id, booking.PaymentOrderId.Value, ct);
        if (lookup.State is not ("PENDING" or "UNKNOWN" or "SETTLED" or "CLOSED_NO_COLLECTION") || lookup.Collections is null)
            throw BookingContracts.InvalidOwner();
        foreach (var collection in lookup.Collections)
        {
            if (collection.CollectionId == Guid.Empty || collection.OrderId != booking.PaymentOrderId || collection.ReservationId != booking.Id ||
                collection.AccountId != booking.AccountId || collection.TenantId != booking.TenantId || collection.SiteId != booking.SiteId ||
                collection.QuoteId != quote.QuoteId || collection.TermsVersion != booking.TermsVersion ||
                collection.Amount != booking.DepositAmount || collection.Currency != booking.Currency) throw BookingContracts.InvalidOwner();
        }
        now = await bookings.DatabaseNowAsync(ct);
        // Duplicate delivery is harmless; contradictory payloads for one financial identity
        // are not. Pin an already-selected collection regardless of producer list ordering.
        var groups = lookup.Collections.GroupBy(x => x.CollectionId).ToArray();
        if (groups.Any(group => group.Distinct().Skip(1).Any()) ||
            lookup.State == "CLOSED_NO_COLLECTION" && groups.Length != 0 ||
            lookup.State == "SETTLED" && groups.Length == 0)
            throw BookingContracts.InvalidOwner();
        var distinct = groups.Select(group => group.First())
            .OrderByDescending(x => x.CollectionId == booking.CollectionId).ThenBy(x => x.CollectionId).ToArray();
        if (booking.CollectionId is not null && distinct.All(x => x.CollectionId != booking.CollectionId))
            throw BookingContracts.InvalidOwner();
        foreach (var extra in distinct.Skip(1))
            await commercial.ReconcileAsync(booking.Id, extra, "ADDITIONAL_COLLECTION", ct);
        if (distinct.FirstOrDefault() is { } paid)
        {
            if (now >= booking.ExpectedStartTime)
            {
                await commercial.ReconcileAsync(booking.Id, paid, "NO_BOOKING_ENTITLEMENT", ct);
                request.FinishBooking("EXPIRED", "PAYMENT_RECONCILIATION_ACCEPTED");
                return;
            }
            await ConfirmAsync(request, booking, paid.CollectionId, ct);
        }
        else if (lookup.State == "CLOSED_NO_COLLECTION" && now >= booking.HoldExpiresAt)
            request.FinishBooking("EXPIRED", "PAYMENT_HOLD_EXPIRED");
        else request.Defer(now.AddSeconds(1), lookup.State == "UNKNOWN" ? "PAYMENT_OUTCOME_UNKNOWN" : "AWAITING_PAYMENT");
    }

    private async Task ConfirmAsync(ReservationRequest request, ReservationRow booking, Guid? collectionId, CancellationToken ct)
    {
        var now = await bookings.DatabaseNowAsync(ct);
        if (now >= booking.ExpectedStartTime) { request.Defer(now.AddSeconds(1), "BOOKING_START_REACHED"); return; }
        var dependency = await RequireDependencyAsync(request, "CONFIRMATION", booking.TenantId, booking.SiteId,
            booking.TargetSlotId!.Value, booking.ExpectedStartTime, now, ct);
        if (dependency is null) return;
        var resourceFacts = JsonSerializer.Deserialize<FacilityResourceSnapshot>(dependency.SnapshotJson!)
            ?? throw new InvalidOperationException("Stored Facility facts are invalid.");
        BookingContracts.ValidateFacility(resourceFacts.Booking, BookingContracts.Selection(
            JsonSerializer.Deserialize<CreateReservationRequest>(request.IntentJson)!));
        await bookings.RequireDurableConfirmationAsync(ct);
        // All competing guards and the Facility dependency row are held. Capture once:
        // use this value for every time-sensitive eligibility check and the stored decision.
        var evaluatedAt = await bookings.DatabaseNowAsync(ct);
        if (evaluatedAt >= booking.ExpectedStartTime || evaluatedAt >= dependency.Deadline)
        {
            await ReleaseExpiredHoldAsync(booking, evaluatedAt, ct);
            request.Defer(evaluatedAt.AddSeconds(1), "BOOKING_START_REACHED");
            return;
        }
        await bookings.RequirePlateAvailableAsync(booking, evaluatedAt, ct);
        await mediator.Send(new RequireReservationAvailabilityCommand(booking, resourceFacts, evaluatedAt), ct);
        booking.ConfirmAtEvaluation(collectionId, evaluatedAt);
        dependency.MarkReady(JsonSerializer.Serialize(new { reservationId = booking.Id, outcome = "CONFIRMED",
            evaluatedAt, confirmationEventId = booking.ConfirmationEffectId }), evaluatedAt);
        await publisher.ConfirmedAsync(new(booking.ConfirmationEffectId!.Value, booking.Id, booking.AccountId,
            booking.TenantId, booking.SiteId, evaluatedAt), ct);
        await publisher.AvailabilityChangedAsync(new(Guid.NewGuid(), evaluatedAt, booking.TenantId,
            booking.SiteId, booking.TargetSlotId!.Value, booking.ExpectedStartTime,
            booking.ClaimEndTime ?? booking.ExpectedEndTime, "CONFIRMED"), ct);
        request.FinishBooking("COMPLETED", null);
        // Outbox, receipt and confirmed state become visible only after the owning commit.
        // Lost acknowledgement replays these durable identities; rollback requires a new evaluation.
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

    private async Task<ReservationFacilityDependency?> RequireDependencyAsync(ReservationRequest request, string purpose,
        Guid tenant, Guid site, Guid slot, DateTimeOffset deadline, DateTimeOffset now, CancellationToken ct)
    {
        var observed = await dependencies.FindByWorkflowPurposeAsync(request.Id, purpose, ct);
        if (observed is null)
        {
            dependencies.Add(ReservationFacilityDependency.Prepare(Guid.NewGuid(), request.Id, purpose,
                tenant, site, slot, request.Fingerprint, deadline, now));
            request.Defer(now.AddSeconds(1), "FACILITY_ACCEPTANCE_PENDING");
            return null;
        }
        var row = await dependencies.LockAsync(observed.Id, ct)
            ?? throw new InvalidOperationException("Facility dependency disappeared.");
        if (row.State == "HELD" && now < row.Deadline) return row;
        request.Defer(now.AddSeconds(1), "FACILITY_ACCEPTANCE_PENDING");
        return null;
    }

    private async Task AbortUnusedDependenciesAsync(Guid workflow, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var purpose in new[] { "ADMISSION", "CONFIRMATION" })
        {
            var observed = await dependencies.FindByWorkflowPurposeAsync(workflow, purpose, ct);
            if (observed is null) continue;
            var row = await dependencies.LockAsync(observed.Id, ct);
            if (row?.State is "PREPARING" or "HELD") row.BeginAbort("BOOKING_NOT_ACCEPTED", now);
        }
    }
}
