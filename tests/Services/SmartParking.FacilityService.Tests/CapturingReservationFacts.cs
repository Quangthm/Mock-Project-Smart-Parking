using SmartParking.Contracts.ReservationPayments;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.FacilityService.Application.Common.Synchronization;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Domain.Entities;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.FacilityService.Tests;

internal sealed class CapturingReservationFacts : IReservationFactsPublisher
{
    public Task EnsureOrderAsync(EnsureReservationOrderV1 command, CancellationToken ct) => throw new NotSupportedException();
    public Task RequestDispositionAsync(RequestReservationDispositionV1 command, CancellationToken ct) => throw new NotSupportedException();
    public Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct) => Task.CompletedTask;
    public List<FacilityStructureAppliedV1> Applied { get; } = [];
    public Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct)
    { Applied.Add(fact); return Task.CompletedTask; }
    public Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct) => Task.CompletedTask;
    public Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct) => Task.CompletedTask;
}
