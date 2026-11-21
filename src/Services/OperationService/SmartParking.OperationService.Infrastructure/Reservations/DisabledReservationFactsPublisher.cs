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

public sealed class DisabledReservationFactsPublisher : IReservationFactsPublisher
{
    public Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
    public Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
    public Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
    public Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
}
