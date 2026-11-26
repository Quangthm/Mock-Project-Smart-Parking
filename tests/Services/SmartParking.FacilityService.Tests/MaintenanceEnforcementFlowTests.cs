using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.CancelMaintenanceWindow;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ExtendMaintenanceWindow;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ScheduleMaintenanceWindow;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Infrastructure.Messaging;
using SmartParking.FacilityService.Tests.TestDoubles;

namespace SmartParking.FacilityService.Tests;

// Option B (Event-driven maintenance): Facility commits every schedule/extension/cancellation
// locally with immediate 'acknowledged' + monotonic revision and emits FacilityMaintenanceChangedV1
// via the transactional outbox, with zero synchronous HTTP dependencies on Operations.
public sealed class MaintenanceEnforcementFlowTests
{
    private static readonly OwnerScope Owner = new(Guid.NewGuid(), Guid.NewGuid());
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid EnforcementRef = Guid.NewGuid();

    private static ScheduleMaintenanceWindowCommandHandler Schedule(FakeWindowStore store) =>
        new(new StubScopeResolver(), new StubOwnerAuthorizer(), store,
            NullLogger<ScheduleMaintenanceWindowCommandHandler>.Instance);

    private static ExtendMaintenanceWindowCommandHandler Extend(FakeWindowStore store) =>
        new(new StubScopeResolver(), new StubOwnerAuthorizer(), store,
            NullLogger<ExtendMaintenanceWindowCommandHandler>.Instance);

    private static CancelMaintenanceWindowCommandHandler Cancel(FakeWindowStore store) =>
        new(new StubScopeResolver(), new StubOwnerAuthorizer(), store,
            NullLogger<CancelMaintenanceWindowCommandHandler>.Instance);

    private static ScheduleMaintenanceWindowCommand NewWindowCommand(Guid? unitId = null, Guid? slotId = null) =>
        new(SiteId, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), "Repaving",
            UnitId: unitId, SlotId: slotId);

    private static MaintenanceWindow ExistingWindow(DateTime? start = null, DateTime? end = null, long revision = 1)
    {
        var windowStart = start ?? DateTime.UtcNow.AddHours(1);
        var windowEnd = end ?? windowStart.AddHours(1);
        return new MaintenanceWindow(Guid.NewGuid(), Owner.TenantId, SiteId, null, null,
            windowStart, windowEnd, "Repaving", Owner.UserId, windowStart.AddDays(-1), null,
            "acknowledged", EnforcementRef, windowStart.AddDays(-1), revision);
    }

    [Fact]
    public async Task Schedule_CommitsAcknowledgedWindowImmediatelyWithoutHttpClient()
    {
        var store = new FakeWindowStore();

        var window = await Schedule(store).Handle(NewWindowCommand(), CancellationToken.None);

        Assert.Equal(1, store.CreateCalls);
        Assert.Equal("acknowledged", window.EnforcementState);
        Assert.NotNull(window.EnforcementRef);
        Assert.NotNull(window.AcknowledgedAt);
        Assert.Equal(1L, window.Revision);
    }

    [Fact]
    public async Task Schedule_UnitAndSlotScope_ArePreserved()
    {
        var store = new FakeWindowStore();
        var unitId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        var window = await Schedule(store).Handle(NewWindowCommand(unitId, slotId), CancellationToken.None);

        Assert.Equal(unitId, window.UnitId);
        Assert.Equal(slotId, window.SlotId);
    }

    [Fact]
    public async Task Extend_IncrementsRevisionAndKeepsAcknowledgedState()
    {
        var store = new FakeWindowStore();
        var existing = ExistingWindow(revision: 1);
        store.Seed(existing);
        var newEnd = existing.End.AddHours(2);

        var updated = await Extend(store).Handle(
            new ExtendMaintenanceWindowCommand(SiteId, existing.Id, null, newEnd), CancellationToken.None);

        Assert.Equal(1, store.ExtendCalls);
        Assert.Equal(newEnd, updated.End);
        Assert.Equal("acknowledged", updated.EnforcementState);
        Assert.Equal(2L, updated.Revision);
    }

    [Fact]
    public async Task Cancel_MarksWindowCancelledAndIncrementsRevision()
    {
        var store = new FakeWindowStore();
        var existing = ExistingWindow(revision: 2);
        store.Seed(existing);

        await Cancel(store).Handle(
            new CancelMaintenanceWindowCommand(SiteId, existing.Id), CancellationToken.None);

        Assert.Equal(1, store.CancelCalls);
        Assert.NotNull(store.Current?.CancelledAt);
        Assert.Equal(3L, store.Current?.Revision);
    }

    [Fact]
    public async Task Cancel_WhenStoreThrowsStructureInUse_PropagatesWithoutSwallowing()
    {
        var store = new FakeWindowStore { ThrowOnCancel = new StructureException("STRUCTURE_IN_USE", "Another live closure.") };
        store.Seed(ExistingWindow());

        var ex = await Assert.ThrowsAsync<StructureException>(() =>
            Cancel(store).Handle(new CancelMaintenanceWindowCommand(SiteId, store.Current!.Id), CancellationToken.None));

        Assert.Equal("STRUCTURE_IN_USE", ex.Code);
    }

    [Fact]
    public async Task BoundaryTicker_NotifiesStoreForDueTransitions()
    {
        var store = new FakeWindowStore();
        var services = new ServiceCollection();
        services.AddSingleton<IMaintenanceWindowStore>(store);
        await using var provider = services.BuildServiceProvider();
        var ticker = new MaintenanceBoundaryTickerWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MaintenanceBoundaryTickerWorker>.Instance);

        await ticker.RunTickAsync(CancellationToken.None);

        Assert.Equal(1, store.NotifyBoundariesCalls);
    }

    [Fact]
    public async Task MassTransitMaintenancePublisher_PublishesUpsertedAndClearedEvents()
    {
        var endpoint = new RecordingPublishEndpoint();
        var publisher = new MassTransitFacilityMaintenancePublisher(endpoint);
        var maintenanceId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddMinutes(30);

        var upserted = new FacilityMaintenanceChangedV1(
            Guid.NewGuid(),
            FacilityMaintenanceTopology.CurrentSchemaVersion,
            maintenanceId,
            1L,
            DateTimeOffset.UtcNow,
            Owner.TenantId,
            SiteId,
            "UPSERTED",
            "SCHEDULED",
            "UNIT",
            unitId,
            [unitId],
            [slotId],
            start,
            null,
            "Planned repaving");

        var cleared = upserted with
        {
            EventId = Guid.NewGuid(),
            Revision = 2L,
            ChangeType = "CLEARED",
            EndAtUtc = start.AddHours(2)
        };

        await publisher.StageChangedAsync(upserted);
        await publisher.StageChangedAsync(cleared);

        Assert.Equal(2, endpoint.MaintenanceChanged.Count);
        var first = endpoint.MaintenanceChanged[0];
        Assert.Equal("UPSERTED", first.ChangeType);
        Assert.Null(first.EndAtUtc);
        Assert.Equal([unitId], first.ScopedUnitIds);
        Assert.Equal([slotId], first.SlotIds);

        var second = endpoint.MaintenanceChanged[1];
        Assert.Equal("CLEARED", second.ChangeType);
        Assert.Equal(2L, second.Revision);
    }

    [Fact]
    public async Task MassTransitMaintenancePublisher_RejectsInvalidEnvelopes()
    {
        var publisher = new MassTransitFacilityMaintenancePublisher(new RecordingPublishEndpoint());
        var valid = new FacilityMaintenanceChangedV1(
            Guid.NewGuid(), 1, Guid.NewGuid(), 1L, DateTimeOffset.UtcNow,
            Owner.TenantId, SiteId, "UPSERTED", "SCHEDULED", "SITE",
            null, [], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "Reason");

        await Assert.ThrowsAsync<ArgumentException>(() => publisher.StageChangedAsync(valid with { EventId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.StageChangedAsync(valid with { MaintenanceId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.StageChangedAsync(valid with { Revision = 0 }));
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.StageChangedAsync(valid with { ChangeType = "INVALID" }));
    }

    [Fact]
    public async Task FacilityMessaging_RegistersNoOpPublisherWhenOutboxDisabled_AndMassTransitPublisherWhenEnabled()
    {
        var disabledServices = new ServiceCollection();
        disabledServices.AddFacilityMessaging(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Messaging:LayoutOutboxEnabled"] = "false" })
            .Build());
        await using (var disabledProvider = disabledServices.BuildServiceProvider())
        {
            var publisher = disabledProvider.GetRequiredService<IFacilityMaintenancePublisher>();
            Assert.IsType<NoOpFacilityMaintenancePublisher>(publisher);
            await publisher.StageChangedAsync(new FacilityMaintenanceChangedV1(
                Guid.Empty, 1, Guid.Empty, 0, DateTimeOffset.UtcNow, Guid.Empty, Guid.Empty,
                "UPSERTED", "SCHEDULED", "SITE", null, [], [], DateTimeOffset.UtcNow, null, "NoOp"));
        }

        var enabledServices = new ServiceCollection();
        enabledServices.AddFacilityMessaging(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:LayoutOutboxEnabled"] = "true",
                ["Messaging:OutboxDeliveryEnabled"] = "false",
                ["RabbitMq:ConnectionString"] = "amqp://user:pass@localhost:5672/smartpark"
            })
            .Build());
        Assert.Contains(enabledServices, d =>
            d.ServiceType == typeof(IFacilityMaintenancePublisher) &&
            d.ImplementationType == typeof(MassTransitFacilityMaintenancePublisher));
        Assert.Equal("smartpark.facility.maintenance-changed.v1", FacilityMaintenanceTopology.ChangedExchangeName);
    }

    // --- Fakes --------------------------------------------------------------------------------

    private sealed class StubScopeResolver : IOwnerScopeResolver
    {
        public Task<OwnerScope> ResolveAsync(Guid? tenantId, CancellationToken ct, string permission = "PARKING_MANAGE") =>
            Task.FromResult(Owner);
    }

    private sealed class StubOwnerAuthorizer : IOwnerAuthorizer
    {
        public Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FakeWindowStore : IMaintenanceWindowStore
    {
        public MaintenanceWindow? Current { get; private set; }
        public int CreateCalls { get; private set; }
        public int ExtendCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public int NotifyBoundariesCalls { get; private set; }
        public StructureException? ThrowOnCancel { get; set; }

        public void Seed(MaintenanceWindow window) => Current = window;

        public Task<IReadOnlyList<MaintenanceWindow>> ListAsync(OwnerScope owner, Guid siteId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MaintenanceWindow>>(Current is null || Current.CancelledAt is not null ? [] : [Current]);

        public Task<MaintenanceWindow?> GetByIdAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct) =>
            Task.FromResult(Current?.Id == windowId && Current.CancelledAt is null ? Current : null);

        public Task<MaintenanceWindow> CreateAsync(OwnerScope owner, Guid siteId, DateTime start, DateTime end,
            string reason, CancellationToken ct, Guid? unitId = null, Guid? slotId = null)
        {
            CreateCalls++;
            var now = DateTime.UtcNow;
            Current = new MaintenanceWindow(Guid.NewGuid(), owner.TenantId, siteId, unitId, slotId, start, end,
                reason, owner.UserId, now, null, "acknowledged", Guid.NewGuid(), now, 1L);
            return Task.FromResult(Current);
        }

        public Task<MaintenanceWindow> ExtendAsync(OwnerScope owner, Guid siteId, Guid windowId, DateTime newEnd, CancellationToken ct)
        {
            ExtendCalls++;
            var now = DateTime.UtcNow;
            Current = Current! with
            {
                End = newEnd,
                EnforcementState = "acknowledged",
                EnforcementRef = Guid.NewGuid(),
                AcknowledgedAt = now,
                Revision = Current.Revision + 1
            };
            return Task.FromResult(Current);
        }

        public Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
        {
            if (ThrowOnCancel is not null) throw ThrowOnCancel;
            CancelCalls++;
            if (Current is not null)
                Current = Current with { CancelledAt = DateTime.UtcNow, Revision = Current.Revision + 1 };
            return Task.CompletedTask;
        }

        public Task<int> NotifyDueBoundariesAsync(DateTime now, CancellationToken ct)
        {
            NotifyBoundariesCalls++;
            return Task.FromResult(0);
        }
    }
}
