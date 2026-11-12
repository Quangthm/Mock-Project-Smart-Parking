using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ProgressReservationHandler(IReservationRequestRepository requests,
    IReservationTermsRepository reviews, IReservationBookingRepository bookings,
    IReservationVehicleOwner vehicles,
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
        if (observed is null || observed.State != "PREPARING") return;
        if (observed.State == "PREPARING" && observed.VehicleBindingState is null)
        {
            await RecordBindingAsync(observed, ct);
            return;
        }
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request is null || request.State != "PREPARING") return;
        var now = await bookings.DatabaseNowAsync(ct);
        try
        {
            await PrepareAsync(request, now, ct);
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
        // Accepted terms are durably stored with the booking; a real Payment order is created
        // only after the hold is committed and the exact reservation identity is known.
        now = await bookings.DatabaseNowAsync(ct);
        if (now >= booking.HoldExpiresAt || now >= request.OverallDeadline)
        { request.Reject("BOOKING_PREPARATION_EXPIRED"); return; }
        review.Accept(request.Id);
        await bookings.AddAsync(booking, ct);
        request.AttachBooking(booking.Id);
        await PublishHoldAsync(booking, now, "PENDING_PAYMENT", booking.Version, ct);
        dependency.MarkReady(JsonSerializer.Serialize(new { reservationId = booking.Id, outcome = "HOLD_COMMITTED" }), now);
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
