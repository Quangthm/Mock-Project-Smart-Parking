using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ConfirmZeroPriceReservationCommand(Guid ReservationId) : IRequest;

public sealed class ConfirmZeroPriceReservationHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IReservationAvailabilityRepository availability,
    IReservationFacilityDependencyRepository dependencies, IOperationsConcurrency guards,
    IUnitOfWork work, IReservationFactsPublisher publisher, IMediator mediator)
    : IRequestHandler<ConfirmZeroPriceReservationCommand>
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public async Task Handle(ConfirmZeroPriceReservationCommand command, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(command.ReservationId, ct);
        if (observed?.RequestId is not { } workflowId) return;
        await using var lease = await workers.TryEnterAsync(workflowId, ct);
        if (lease is null) return;

        await using var transaction = await work.BeginTransactionAsync(ct);
        await availability.LockAccountAsync(observed.AccountId, ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
        if (booking is null || booking.DepositAmount != 0 || booking.ConfirmationState != "PENDING") return;

        var now = await bookings.DatabaseNowAsync(ct);
        await ReleaseExpiredHoldAsync(booking, now, ct);
        if (now >= booking.ExpectedStartTime)
        {
            booking.CloseConfirmation("BOOKING_START_REACHED");
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        var selection = new BookingSelection(booking.VehicleId!.Value, booking.SiteId, booking.TargetSlotId!.Value,
            booking.ExpectedStartTime, booking.ExpectedEndTime, booking.SelectedPaymentMethod!);
        var dependency = await dependencies.FindByWorkflowPurposeAsync(workflowId, "CONFIRMATION", ct);
        if (dependency is null)
        {
            dependencies.Add(ReservationFacilityDependency.Prepare(Guid.NewGuid(), workflowId, "CONFIRMATION",
                booking.TenantId, booking.SiteId, booking.TargetSlotId.Value, BookingContracts.Fingerprint(selection),
                booking.ExpectedStartTime, now));
            booking.DeferConfirmation(now.Add(RetryDelay), "FACILITY_ACCEPTANCE_PENDING");
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        dependency = await dependencies.LockAsync(dependency.Id, ct)
            ?? throw new InvalidOperationException("Confirmation Facility dependency disappeared.");
        if (dependency.State != "HELD" || now >= dependency.Deadline)
        {
            booking.DeferConfirmation(now.Add(RetryDelay), "FACILITY_ACCEPTANCE_PENDING");
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        var resourceFacts = JsonSerializer.Deserialize<FacilityResourceSnapshot>(dependency.SnapshotJson!)
            ?? throw new InvalidOperationException("Stored Facility facts are invalid.");
        BookingContracts.ValidateFacility(resourceFacts.Booking, selection);
        await bookings.RequireDurableConfirmationAsync(ct);
        var evaluatedAt = await bookings.DatabaseNowAsync(ct);
        if (evaluatedAt >= booking.ExpectedStartTime || evaluatedAt >= dependency.Deadline)
        {
            await ReleaseExpiredHoldAsync(booking, evaluatedAt, ct);
            booking.CloseConfirmation("BOOKING_START_REACHED");
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        try
        {
            await bookings.RequirePlateAvailableAsync(booking, evaluatedAt, ct);
            await mediator.Send(new RequireReservationAvailabilityCommand(booking, resourceFacts, evaluatedAt), ct);
        }
        catch (ReservationGateException error) when (error.StatusCode == 409)
        {
            await ReleaseForUnavailableConfirmationAsync(booking, error.Code, evaluatedAt, ct);
            booking.CloseConfirmation(error.Code);
            await work.SaveChangesAsync(ct);
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

        booking.ConfirmAtEvaluation(null, evaluatedAt);
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

public sealed record RecordReservationDepositCollectedCommand(ReservationDepositCollectedV1 Event) : IRequest;

public sealed class RecordReservationDepositCollectedHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IOperationsConcurrency guards, IUnitOfWork work,
    IMediator mediator) : IRequestHandler<RecordReservationDepositCollectedCommand>
{
    public async Task Handle(RecordReservationDepositCollectedCommand command, CancellationToken ct)
    {
        var payment = command.Event;
        if (payment.ReservationId == Guid.Empty || payment.PaymentOrderId == Guid.Empty ||
            payment.AccountId == Guid.Empty || payment.TenantId == Guid.Empty || payment.SiteId == Guid.Empty ||
            payment.PaymentTransactionId == Guid.Empty)
            return;

        var observed = await bookings.FindOrderProvisioningAsync(payment.ReservationId, ct);
        if (observed?.RequestId is not { } workflowId) return;

        var shouldConfirm = false;
        await using (var lease = await workers.TryEnterAsync(workflowId, ct))
        {
            if (lease is null) return;

            await using var transaction = await work.BeginTransactionAsync(ct);
            await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
            await guards.LockSiteAsync(observed.SiteId, ct);
            var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
            if (booking is null || booking.ConfirmationEvaluatedAt is not null)
            {
                await transaction.CommitAsync(ct);
                return;
            }

            var now = await bookings.DatabaseNowAsync(ct);
            if (booking.CollectionId == payment.CollectionId)
            {
                await transaction.CommitAsync(ct);
                return;
            }

            try
            {
                BookingContracts.ValidateReservationCollectionBinding(booking, payment);
            }
            catch (InvalidOperationException)
            {
                // Contract blocker: PaymentService has not published an approved confirmation-decline
                // outcome contract, so a mismatched collection is not allowed to mutate the live hold.
                await transaction.CommitAsync(ct);
                return;
            }

            if (booking.Status == "PENDING_PAYMENT" && booking.HoldExpiresAt <= now)
                booking.ReleaseExpiredHold(now);
            if (booking.Status is not ("PENDING_PAYMENT" or "EXPIRED") || booking.CollectionId is not null)
            {
                await transaction.CommitAsync(ct);
                return;
            }

            shouldConfirm = booking.BeginPaidConfirmation(payment.CollectionId, now);
            if (shouldConfirm)
            {
                await work.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }

        if (shouldConfirm)
            await mediator.Send(new ConfirmPaidReservationCommand(payment.ReservationId, payment.CollectionId), ct);
    }
}

public sealed class RecordReservationPaymentHandler(IReservationBookingRepository bookings, IMediator mediator)
    : IRequestHandler<RecordReservationPaymentCommand>
{
    public async Task Handle(RecordReservationPaymentCommand command, CancellationToken ct)
    {
        var payment = command.Evidence;
        if (payment.EventId == Guid.Empty || payment.PaymentOrderId == Guid.Empty ||
            payment.PaymentTransactionId == Guid.Empty || payment.ReservationId == Guid.Empty ||
            payment.AccountId == Guid.Empty || payment.TenantId == Guid.Empty || payment.SiteId == Guid.Empty)
            return;

        var observed = await bookings.FindOrderProvisioningAsync(payment.ReservationId, ct);
        if (observed is null) return;

        if (observed.RequestId is not { } operationId || observed.TermsId is not { } termsId ||
            string.IsNullOrWhiteSpace(observed.TermsVersion))
            throw new InvalidOperationException("Reservation payment evidence arrived before accepted terms were persisted.");

        if (observed.PaymentOrderId is null)
            throw new InvalidOperationException("Reservation payment evidence arrived before the order binding was persisted.");
        if (observed.PaymentOrderId != payment.PaymentOrderId)
            throw new InvalidOperationException("Reservation payment evidence references a different order.");

        if (observed.AccountId != payment.AccountId || observed.TenantId != payment.TenantId ||
            observed.SiteId != payment.SiteId || observed.DepositAmount is null or <= 0 ||
            observed.DepositAmount != payment.Amount ||
            !string.Equals(observed.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reservation payment evidence does not match the persisted booking binding.");

        var terms = BookingContracts.AcceptedTerms(observed);
        var collected = new ReservationDepositCollectedV1(payment.EventId, payment.PaymentTransactionId,
            payment.PaymentOrderId, payment.PaymentTransactionId, operationId, observed.Id, observed.AccountId,
            observed.TenantId, observed.SiteId, terms.QuoteId, terms.Version, termsId, observed.TermsVersion,
            payment.Amount, payment.Currency, payment.Provider, payment.ProviderReference ?? string.Empty,
            payment.PaidAt, payment.PaidAt);

        await mediator.Send(new RecordReservationDepositCollectedCommand(collected), ct);
    }
}

public sealed record ConfirmPaidReservationCommand(Guid ReservationId, Guid CollectionId) : IRequest;

public sealed class ConfirmPaidReservationHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IReservationAvailabilityRepository availability,
    IReservationFacilityDependencyRepository dependencies, IOperationsConcurrency guards,
    IUnitOfWork work, IReservationFactsPublisher publisher, IMediator mediator,
    IReservationCommercialOwner commercial)
    : IRequestHandler<ConfirmPaidReservationCommand>
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public async Task Handle(ConfirmPaidReservationCommand command, CancellationToken ct)
    {
        var observed = await bookings.FindOrderProvisioningAsync(command.ReservationId, ct);
        if (observed?.RequestId is not { } workflowId) return;
        await using var lease = await workers.TryEnterAsync(workflowId, ct);
        if (lease is null) return;

        await using var transaction = await work.BeginTransactionAsync(ct);
        await availability.LockAccountAsync(observed.AccountId, ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        var booking = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
        if (booking is null || booking.DepositAmount is null or <= 0 || booking.CollectionId != command.CollectionId)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var now = await bookings.DatabaseNowAsync(ct);
        await ReleaseExpiredHoldAsync(booking, now, ct);
        if (now >= booking.ExpectedStartTime)
        {
            booking.CloseConfirmation("BOOKING_START_REACHED");
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        var selection = new BookingSelection(booking.VehicleId!.Value, booking.SiteId, booking.TargetSlotId!.Value,
            booking.ExpectedStartTime, booking.ExpectedEndTime, booking.SelectedPaymentMethod!);
        var dependency = await dependencies.FindByWorkflowPurposeAsync(workflowId, "CONFIRMATION", ct);
        if (dependency is null)
        {
            dependencies.Add(ReservationFacilityDependency.Prepare(Guid.NewGuid(), workflowId, "CONFIRMATION",
                booking.TenantId, booking.SiteId, booking.TargetSlotId.Value, BookingContracts.Fingerprint(selection),
                booking.ExpectedStartTime, now));
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

        var resourceFacts = System.Text.Json.JsonSerializer.Deserialize<FacilityResourceSnapshot>(dependency.SnapshotJson!)
            ?? throw new InvalidOperationException("Stored Facility facts are invalid.");
        BookingContracts.ValidateFacility(resourceFacts.Booking, selection);
        await bookings.RequireDurableConfirmationAsync(ct);
        var evaluatedAt = await bookings.DatabaseNowAsync(ct);
        if (evaluatedAt >= booking.ExpectedStartTime || evaluatedAt >= dependency.Deadline)
        {
            await ReleaseExpiredHoldAsync(booking, evaluatedAt, ct);
            booking.CloseConfirmation("BOOKING_START_REACHED");
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        try
        {
            await bookings.RequirePlateAvailableAsync(booking, evaluatedAt, ct);
            await mediator.Send(new RequireReservationAvailabilityCommand(booking, resourceFacts, evaluatedAt), ct);
        }
        catch (ReservationGateException error) when (error.StatusCode == 409)
        {
            var reason = error.Code;
            var failedCollection = new VerifiedCollection(command.CollectionId, booking.PaymentOrderId!.Value,
                booking.Id, booking.AccountId, booking.TenantId, booking.SiteId,
                BookingContracts.AcceptedTerms(booking).QuoteId, booking.TermsVersion!,
                booking.DepositAmount ?? 0m, booking.Currency ?? "VND");
            await ReleaseForUnavailableConfirmationAsync(booking, reason, evaluatedAt, ct);
            booking.CloseConfirmation(reason);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await commercial.ReconcileAsync(booking.Id, failedCollection, reason, ct);
            return;
        }
        catch (ReservationGateException error) when (error.StatusCode == 503)
        {
            booking.DeferConfirmation(evaluatedAt.Add(RetryDelay), error.Code);
            await work.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return;
        }

        booking.ConfirmAtEvaluation(command.CollectionId, evaluatedAt);
        dependency.MarkReady(System.Text.Json.JsonSerializer.Serialize(new
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
