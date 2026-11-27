using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class CommitQuoteHoldHandler(IReservationRequestRepository requests, IReservationBookingRepository bookings,
    IReservationQuoteOwner payment, IStructureRepository structure,
    IReservationAvailabilityRepository availability, IOperationsConcurrency guards, IUnitOfWork work,
    IReservationSubscriptionEligibility subscriptions, IReservationFactsPublisher facts, IMediator mediator,
    IAdmissionMessages? saga = null) : IRequestHandler<CommitQuoteHoldCommand>
{
    public async Task Handle(CommitQuoteHoldCommand command, CancellationToken ct)
    {
        var observed = await requests.FindForWorkerAsync(command.RequestId, ct);
        if (observed?.State != "EVALUATING") return;
        var intent = JsonSerializer.Deserialize<QuoteReservationIntent>(observed.IntentJson)
            ?? throw new InvalidOperationException("Missing admitted quote intent.");
        PaymentReservationQuote? proof = null;
        ReservationGateException? failure = null;
        try
        {
            proof = await payment.ReadProofAsync(observed.AccountId, intent.QuoteId, ct);
        }
        catch (ReservationGateException error) { failure = error; }

        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(observed.Id, ct);
        if (request?.State != "EVALUATING") return;
        if (request.IntentJson != observed.IntentJson || request.AccountId != observed.AccountId ||
            request.AccountCheck != "SUCCEEDED" || request.PlateCheck != "SUCCEEDED" ||
            request.SlotCheck != "SUCCEEDED" || request.DecisionId is null)
            throw new InvalidOperationException("Admitted intent or joined owner evidence is inconsistent.");
        var now = await bookings.DatabaseNowAsync(ct);
        try
        {
            RequireTimely(request, intent, now);
            if (failure is not null) throw failure;
            if (proof?.Binding is null) throw BookingContracts.InvalidOwner();
            if (proof.QuoteId != intent.QuoteId ||
                proof.Binding.AccountId != request.AccountId || proof.Binding.CanonicalPlate != request.VehiclePlate ||
                request.VehiclePlate != intent.LicensePlate || proof.Binding.RequestedSlotId != intent.RequestedSlotId ||
                proof.Binding.TenantId != request.ResolvedTenantId || proof.Binding.SiteId != request.ResolvedSiteId ||
                proof.Binding.Start != intent.Start || proof.Binding.End != intent.End || proof.Binding.PaymentMethod != intent.PaymentMethod)
                throw BookingContracts.InvalidOwner();
            if (proof.Binding.Coverage is null || proof.Binding.SiteId == Guid.Empty || proof.Binding.TenantId == Guid.Empty)
                throw BookingContracts.InvalidOwner();
            await AcceptAsync(request, intent, proof, ct);
        }
        catch (ReservationPolicyUnavailableException)
        {
            var decisionTime = await bookings.DatabaseNowAsync(ct);
            if (decisionTime >= request.OverallDeadline || decisionTime >= intent.Start) request.Reject("BOOKING_PREPARATION_EXPIRED");
            else request.FailQuoteAdmission("RESERVATION_POLICY_UNAVAILABLE");
        }
        catch (ReservationGateException error) when (error.StatusCode is 400 or 404 or 409 or 503)
        {
            var decisionTime = await bookings.DatabaseNowAsync(ct);
            if (decisionTime >= request.OverallDeadline || decisionTime >= intent.Start) request.Reject("BOOKING_PREPARATION_EXPIRED");
            else if (error.StatusCode == 503) request.FailQuoteAdmission(error.Code);
            else request.Reject(error.Code);
        }

        if (request.State != "EVALUATING")
            await (saga ?? throw new InvalidOperationException("Admission Saga unavailable.")).EvaluateAsync(request.Id, ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task AcceptAsync(ReservationRequest request, QuoteReservationIntent intent, PaymentReservationQuote proof,
        CancellationToken ct)
    {
        var binding = proof.Binding;
        await availability.LockAccountAsync(request.AccountId, ct);
        await guards.LockVehicleAsync(intent.LicensePlate, ct);
        await guards.LockSiteAsync(binding.SiteId, ct);
        var now = await bookings.DatabaseNowAsync(ct);
        RequireTimely(request, intent, now);
        proof.ValidateFor(binding, now);
        if (await structure.HoldAsync(binding.SiteId, ct) is not null)
        {
            RequireTimely(request, intent, await bookings.DatabaseNowAsync(ct));
            request.Reject("STRUCTURE_BUSY");
            return;
        }
        var site = await structure.StateAsync(binding.SiteId, ct);
        if (site is not null && (site.TenantId != binding.TenantId || !site.Active))
            throw new ReservationGateException(409, "SLOT_UNAVAILABLE", "The selected site is unavailable.");
        var interval = new ReservationInterval(intent.Start, intent.End);
        var policy = await mediator.Send(new ResolveReservationPoliciesQuery(binding.TenantId, binding.SiteId, now), ct);
        var expected = binding with { Coverage = subscriptions.Evaluate(intent.LicensePlate, binding.TenantId, binding.SiteId, interval),
            PlatformPolicyId = policy.Platform.Id, PlatformPolicyVersion = policy.Platform.Version,
            SitePolicyId = policy.Site.Id, SitePolicyVersion = policy.Site.Version };
        if (expected != binding || !policy.AllowsWindow(interval, now)) throw Stale();
        await mediator.Send(new RequireBookingPermissionCommand(request.AccountId, policy, now), ct);
        if (await bookings.HasQuoteBindingAsync(proof.QuoteId, ct))
            throw new ReservationGateException(409, "QUOTE_ALREADY_BOUND", "The quote already has a reservation.");
        var holdUntil = proof.DepositAmount == 0 ? (DateTimeOffset?)null : policy.DeadlinesFor(interval, now).PaymentDueAt;
        if (holdUntil is DateTimeOffset h) holdUntil = h.AddTicks(-(h.UtcTicks % 10));
        var row = ReservationRow.PrepareQuote(request.Id, request.AccountId, intent.LicensePlate, binding.TenantId,
            binding.SiteId, intent.RequestedSlotId, intent.PaymentMethod, intent.Start, intent.End,
            intent.End.AddMinutes(policy.Values["INTER_BOOKING_BUFFER"]), now, holdUntil,
            proof.BookedAmount, proof.CoveredAmount, proof.DepositAmount, proof.Currency, JsonSerializer.Serialize(policy),
            proof.QuoteId, proof.ValidUntil, proof.TariffVersion, binding.Coverage.Version, binding.Coverage.IsCovered);
        await bookings.RequirePlateAvailableAsync(row, now, ct);
        await mediator.Send(new RequireReservationAvailabilityCommand(row, now), ct);
        var checkpoint = await bookings.DatabaseNowAsync(ct);
        RequireTimely(request, intent, checkpoint);
        if (checkpoint >= row.HoldExpiresAt || checkpoint >= proof.ValidUntil) throw Stale();

        var finalPolicy = await mediator.Send(new ResolveReservationPoliciesQuery(binding.TenantId, binding.SiteId, checkpoint), ct);
        var confirmed = proof.DepositAmount == 0;
        if (confirmed) await bookings.RequireDurableConfirmationAsync(ct);
        checkpoint = await bookings.DatabaseNowAsync(ct);
        RequireTimely(request, intent, checkpoint);
        if (checkpoint >= row.HoldExpiresAt || checkpoint >= proof.ValidUntil ||
            finalPolicy.Platform != policy.Platform || finalPolicy.Site != policy.Site || !finalPolicy.AllowsWindow(interval, checkpoint)) throw Stale();
        if (confirmed)
        {
            row.ConfirmZeroAdmission(checkpoint);
        }
        await bookings.AddAsync(row, ct);
        request.CompleteAdmission(row.Id, confirmed);
        await facts.EnsureOrderAsync(new(row.Id, row.AccountId, binding.TenantId, binding.SiteId,
            proof.QuoteId, binding.RequestedSlotId, binding.CanonicalPlate, binding.Start, binding.End,
            binding.PaymentMethod, proof.DepositAmount, proof.Currency, binding.Coverage.IsCovered,
            binding.Coverage.Version, row.HoldExpiresAt), ct);
        if (confirmed)
        {
            await facts.ConfirmedAsync(new(row.ConfirmationEffectId!.Value, row.Id, row.AccountId, row.TenantId, row.SiteId, checkpoint), ct);
            await facts.AvailabilityChangedAsync(new(Guid.NewGuid(), checkpoint, row.TenantId, row.SiteId,
                binding.RequestedSlotId, binding.Start, row.ClaimEndTime!.Value, row.Status), ct);
            return;
        }
        await (saga ?? throw new InvalidOperationException("Admission messaging unavailable."))
            .RegisterDeadlineAsync(new(ReservationDeadlineKind.UnpaidHold, row.Id, row.HoldExpiresAt!.Value), ct);
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
