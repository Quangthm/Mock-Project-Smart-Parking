using SmartParking.Contracts.ReservationPayments;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MassTransit;
using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Infrastructure.Messaging;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class DisabledReservationFactsPublisher(ILogger<DisabledReservationFactsPublisher>? logger = null) : IReservationFactsPublisher
{
    public Task EnsureOrderAsync(EnsureReservationOrderV1 command, CancellationToken ct) => throw Unavailable();
    public Task RequestDispositionAsync(RequestReservationDispositionV1 command, CancellationToken ct) => throw Unavailable();
    private static ReservationGateException Unavailable() => new(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Reservation messaging is disabled.");
    public Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
    public Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct)
    {
        // Durable reservation messaging is disabled, so this structure fact cannot be published.
        // Skipping publication is safe and must not fail the owning structure-edit workflow, so
        // this is logged as a warning and treated as a no-op rather than failing closed.
        logger?.LogWarning(
            "Durable reservation messaging is disabled; skipping FacilityStructureAppliedV1 publication for SiteId {SiteId} (Token {Token}).",
            fact.SiteId,
            fact.SynchronizationToken);
        return Task.CompletedTask;
    }
    public Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
    public Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
}
