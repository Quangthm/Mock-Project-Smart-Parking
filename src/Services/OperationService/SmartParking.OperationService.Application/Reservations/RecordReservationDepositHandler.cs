using System.Text.Json;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class RecordReservationDepositHandler(IReservationBookingRepository bookings,
    IReservationWorkflowConcurrency workers, IOperationsConcurrency guards, IUnitOfWork work,
    IMediator mediator, IReservationCommercialOwner commercial) : IRequestHandler<RecordReservationDepositCommand>
{
    public async Task Handle(RecordReservationDepositCommand command, CancellationToken ct)
    {
        var payment = command.Evidence;
        var observed = await bookings.FindOrderProvisioningAsync(payment.ReservationId, ct);
        if (observed?.RequestId is not { } workflowId)
        {
            await commercial.ReconcileAsync(payment.ReservationId, payment.Collection(), "RESERVATION_NOT_FOUND", ct);
            return;
        }
        string? reconcile = null;
        var confirm = false;
        await using (var lease = await workers.TryEnterAsync(workflowId, ct))
        {
            if (lease is null) throw new InvalidOperationException("Collection delivery must retry the busy booking.");
            await using var transaction = await work.BeginTransactionAsync(ct);
            await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
            await guards.LockSiteAsync(observed.SiteId, ct);
            var row = await bookings.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct);
            if (row is null) return;
            if (row.PaymentOrderId is null && row.OrderProvisioningState is "IN_PROGRESS" or "PENDING" or "UNKNOWN")
                throw new InvalidOperationException("Order acknowledgement is pending.");
            try { BookingContracts.ValidateReservationCollectionBinding(row, payment); }
            catch (InvalidOperationException) { reconcile = "COLLECTION_BINDING_CONFLICT"; }
            // Event and delivery times can change across recovery envelopes. Financial identity cannot.
            var fingerprint = FacilityDependencyProtocol.Fingerprint(JsonSerializer.Serialize(new {
                payment.PaymentTransactionId, payment.Provider, payment.ProviderReference,
                VerifiedAt = payment.ProviderVerifiedAt.ToUniversalTime() }));
            if (reconcile is null && row.CollectionId == payment.CollectionId)
            {
                if (row.CollectionFingerprint is not null && row.CollectionFingerprint != fingerprint)
                    reconcile = "COLLECTION_BINDING_CONFLICT";
                else return; // First decision and its continuation are durable together.
            }
            if (reconcile is null)
            {
                confirm = row.BeginPaidConfirmation(payment.CollectionId, await bookings.DatabaseNowAsync(ct), fingerprint);
                if (confirm) await work.SaveChangesAsync(ct);
                else reconcile = "RESERVATION_NOT_ELIGIBLE";
            }
            await transaction.CommitAsync(ct);
        }
        if (reconcile is not null)
            await commercial.ReconcileAsync(payment.ReservationId, payment.Collection(), reconcile, ct);
        else if (confirm)
            await mediator.Send(new ConfirmPaidReservationCommand(payment.ReservationId, payment.CollectionId), ct);
    }
}
