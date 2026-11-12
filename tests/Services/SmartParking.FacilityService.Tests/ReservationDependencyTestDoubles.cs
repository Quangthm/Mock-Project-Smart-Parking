using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.FacilityService.Application.Common.Synchronization;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Domain.Entities;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.FacilityService.Tests;

internal static class StructureTestMediator
{
    public static IMediator Create(StructureSynchronizationHandlers handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(StructureTestMediator).Assembly));
        services.AddSingleton<IRequestHandler<RecoverStructureSiteCommand>>(handler);
        services.AddSingleton<IRequestHandler<PublishStructureOutcomeCommand>>(handler);
        services.AddSingleton<IRequestHandler<RecoverPendingStructuresCommand, IReadOnlyList<(Guid SiteId, Exception Error)>>>(handler);
        return new Mediator(services.BuildServiceProvider());
    }
}

// Existing structure workflow fixtures have no reverse reservation dependencies.
internal sealed class EmptyReservationDependencies : IReservationDependencyRepository
{
    public bool Held { get; set; }
    public Task<bool> HasHeldAsync(Guid siteId, CancellationToken ct) => Task.FromResult(Held);
    public Task LockAsync(Guid token, Guid siteId, CancellationToken ct) => throw new NotSupportedException();
    public Task<ReservationDependency?> FindAsync(Guid token, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasPendingStructureEditAsync(Guid siteId, CancellationToken ct) => throw new NotSupportedException();
    public Task<ResourceSnapshot> SnapshotAsync(Guid siteId, Guid slotId, CancellationToken ct) => throw new NotSupportedException();
    public Task<DateTimeOffset> NowAsync(CancellationToken ct) => throw new NotSupportedException();
    public void Add(ReservationDependency dependency) => throw new NotSupportedException();
}

internal sealed class CapturingReservationFacts : IReservationFactsPublisher
{
    public Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct) => Task.CompletedTask;
    public List<FacilityStructureAppliedV1> Applied { get; } = [];
    public Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct)
    { Applied.Add(fact); return Task.CompletedTask; }
    public Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct) => Task.CompletedTask;
    public Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct) => Task.CompletedTask;
}
