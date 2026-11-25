using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class CommitQuoteHoldHandler(IReservationRequestRepository requests, IReservationBookingRepository bookings,
    IReservationQuoteOwner payment, IReservationFacilityDependencyRepository dependencies,
    IReservationAvailabilityRepository availability, IOperationsConcurrency guards, IUnitOfWork work,
    IReservationSubscriptionEligibility subscriptions, IReservationFactsPublisher facts, IMediator mediator,
    IAdmissionMessages? saga = null) : IRequestHandler<CommitQuoteHoldCommand>
{
    public async Task Handle(CommitQuoteHoldCommand command, CancellationToken ct)
    {
        dependencies.RequireNoTransaction();
        var observed = await requests.FindForWorkerAsync(command.RequestId, ct);
        if (observed?.State != "EVALUATING") return;
        var intent = JsonSerializer.Deserialize<QuoteReservationIntent>(observed.IntentJson)
            ?? throw new InvalidOperationException("Missing admitted quote intent.");
        PaymentReservationQuote? proof = null;
        ReservationGateException? failure = null;
        try
        {
            proof = await payment.ReadProofAsync(observed.AccountId, intent.QuoteId, intent.QuoteVersion, ct);
        }
        catch (ReservationGateException error) { failure = error; }

        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(observed.Id, ct);
        if (request?.State != "EVALUATING") return;
        var token = await dependencies.FindByWorkflowPurposeAsync(request.Id, "ADMISSION", ct);
        if (token is not null) token = await dependencies.LockAsync(token.Id, ct);
        var now = await bookings.DatabaseNowAsync(ct);
        try
        {
            RequireTimely(request, intent, now);
            if (failure is not null) throw failure;
            if (proof?.Binding is null) throw BookingContracts.InvalidOwner();
            if (proof.QuoteId != intent.QuoteId || proof.QuoteVersion != intent.QuoteVersion ||
                proof.Binding.AccountId != request.AccountId || proof.Binding.CanonicalPlate != request.VehiclePlate ||
                request.VehiclePlate != intent.LicensePlate || proof.Binding.RequestedSlotId != intent.RequestedSlotId ||
                proof.Binding.TenantId != request.ResolvedTenantId || proof.Binding.SiteId != request.ResolvedSiteId ||
                proof.Binding.Start != intent.Start || proof.Binding.End != intent.End || proof.Binding.PaymentMethod != intent.PaymentMethod)
                throw BookingContracts.InvalidOwner();
            if (proof.Binding.Coverage is null) throw BookingContracts.InvalidOwner();
            proof.ValidateFor(proof.Binding, now);
            if (token is null)
            {
                var deadline = request.OverallDeadline < intent.Start ? request.OverallDeadline : intent.Start;
                dependencies.Add(ReservationFacilityDependency.Prepare(Guid.NewGuid(), request.Id, "ADMISSION",
                    proof.Binding.TenantId, proof.Binding.SiteId, intent.RequestedSlotId, request.Fingerprint, deadline, now));
                request.Defer(request.OverallDeadline, "FACILITY_ACCEPTANCE_PENDING");
            }
            else if (token.State == "PREPARING") request.Defer(request.OverallDeadline, "FACILITY_ACCEPTANCE_PENDING");
            else if (token.State == "HELD" && now < token.Deadline)
                await AcceptAsync(request, intent, proof, token, ct);
            else throw Stale();
        }
        catch (ReservationPolicyUnavailableException)
        {
            request.FailQuoteAdmission("RESERVATION_POLICY_UNAVAILABLE");
            if (token?.State is "HELD" or "PREPARING") token.BeginAbort("RESERVATION_POLICY_UNAVAILABLE", now);
        }
        catch (ReservationGateException error) when (error.StatusCode is 400 or 404 or 409 or 503)
        {
            if (error.StatusCode == 503) request.FailQuoteAdmission(error.Code);
            else request.Reject(error.Code);
            if (token?.State is "HELD" or "PREPARING") token.BeginAbort(error.Code, now);
        }

        if (request.State != "EVALUATING")
            await (saga ?? throw new InvalidOperationException("Admission Saga unavailable.")).EvaluateAsync(request.Id, ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task AcceptAsync(ReservationRequest request, QuoteReservationIntent intent, PaymentReservationQuote proof,
        ReservationFacilityDependency token, CancellationToken ct)
    {
        var resources = JsonSerializer.Deserialize<FacilityResourceSnapshot>(token.SnapshotJson!)
            ?? throw BookingContracts.InvalidOwner();
        var binding = proof.Binding;
        await availability.LockAccountAsync(request.AccountId, ct);
        await guards.LockVehicleAsync(intent.LicensePlate, ct);
        await guards.LockSiteAsync(binding.SiteId, ct);
        var now = await bookings.DatabaseNowAsync(ct);
        var interval = new ReservationInterval(intent.Start, intent.End);
        var current = resources.Booking;
        if (!current.EligibleCarSlot || !current.ParticipatesInStructureGuard ||
            current.SiteId != binding.SiteId || current.TenantId != binding.TenantId || current.SlotId != intent.RequestedSlotId) throw Stale();
        var policy = await mediator.Send(new ResolveReservationPoliciesQuery(binding.TenantId, binding.SiteId, now), ct);
        var expected = binding with { Coverage = subscriptions.Evaluate(intent.LicensePlate, binding.TenantId, binding.SiteId, interval),
            PlatformPolicyId = policy.Platform.Id, PlatformPolicyVersion = policy.Platform.Version,
            SitePolicyId = policy.Site.Id, SitePolicyVersion = policy.Site.Version };
        if (expected != binding || !policy.AllowsWindow(interval, now)) throw Stale();
        proof.ValidateFor(expected, now);
        await mediator.Send(new RequireBookingPermissionCommand(request.AccountId, policy, now), ct);
        if (await bookings.HasQuoteBindingAsync(proof.QuoteId, ct))
            throw new ReservationGateException(409, "QUOTE_ALREADY_BOUND", "The quote already has a reservation.");
        var row = ReservationRow.PrepareQuote(request.Id, request.AccountId, intent.LicensePlate, binding.TenantId,
            binding.SiteId, intent.RequestedSlotId, intent.PaymentMethod, intent.Start, intent.End,
            intent.End.AddMinutes(policy.Values["INTER_BOOKING_BUFFER"]), now, policy.DeadlinesFor(interval, now).PaymentDueAt,
            proof.BookedAmount, proof.CoveredAmount, proof.DepositAmount, proof.Currency, JsonSerializer.Serialize(policy),
            proof.QuoteId, proof.QuoteVersion, proof.ValidUntil, proof.TariffVersion, binding.Coverage.Version, binding.Coverage.IsCovered);
        await bookings.RequirePlateAvailableAsync(row, now, ct);
        await mediator.Send(new RequireReservationAvailabilityCommand(row, resources, now), ct);
        var checkpoint = await bookings.DatabaseNowAsync(ct);
        RequireTimely(request, intent, checkpoint);
        if (checkpoint >= token.Deadline || checkpoint >= row.HoldExpiresAt || checkpoint >= proof.ValidUntil) throw Stale();

        var finalPolicy = await mediator.Send(new ResolveReservationPoliciesQuery(binding.TenantId, binding.SiteId, checkpoint), ct);
        if (finalPolicy.Platform != policy.Platform || finalPolicy.Site != policy.Site ||
            !finalPolicy.AllowsWindow(interval, checkpoint)) throw Stale();

        await bookings.AddAsync(row, ct);
        request.AttachQuoteHold(row.Id);
        token.MarkReady(JsonSerializer.Serialize(new { reservationId = row.Id, outcome = "HOLD_COMMITTED" }), checkpoint);
        await facts.HoldChangedAsync(new(Guid.NewGuid(), checkpoint, request.Id, row.Id, request.AccountId,
            binding.TenantId, binding.SiteId, intent.RequestedSlotId, intent.Start, row.ClaimEndTime!.Value,
            row.HoldExpiresAt!.Value, row.Status, row.Version), ct);
    }

    private static ReservationGateException Stale() => new(409, "QUOTE_STALE", "Review the selection again.");

    private static void RequireTimely(ReservationRequest request, QuoteReservationIntent intent, DateTimeOffset now)
    {
        if (now >= request.OverallDeadline || now >= intent.Start)
            throw new ReservationGateException(409, "BOOKING_PREPARATION_EXPIRED", "The admission deadline has passed.");
    }
}
