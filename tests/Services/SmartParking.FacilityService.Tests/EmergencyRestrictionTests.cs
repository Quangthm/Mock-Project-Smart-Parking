using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.API.Controllers;
using SmartParking.FacilityService.API.Middleware;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Helpers;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Infrastructure.Messaging;
using SmartParking.FacilityService.Tests.TestDoubles;

namespace SmartParking.FacilityService.Tests;

// Phase 3 emergency closures: scope validation, integration-event payloads, operator endpoints and
// the read-only projection that withdraws capacity immediately while never displacing a parked car.
public sealed class EmergencyRestrictionTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FloorA = Guid.NewGuid();
    private static readonly Guid FloorB = Guid.NewGuid();
    private static readonly Guid ZoneA = Guid.NewGuid();
    private static readonly Guid BlockInZoneA = Guid.NewGuid();

    private static readonly IReadOnlyDictionary<Guid, Guid?> UnitParents =
        new Dictionary<Guid, Guid?>
        {
            [FloorA] = null,
            [FloorB] = null,
            [ZoneA] = FloorA,
            [BlockInZoneA] = ZoneA
        };

    // ---------------------------------------------------------------- rules

    [Theory]
    [InlineData("EMERGENCY", "SITE", false, false)]
    [InlineData("EMERGENCY", "UNIT", true, false)]
    [InlineData("EMERGENCY", "SLOT", true, true)]
    [InlineData("FORCED_MAINTENANCE", "SITE", false, false)]
    [InlineData("FORCED_MAINTENANCE", "UNIT", true, false)]
    [InlineData("FORCED_MAINTENANCE", "SLOT", true, true)]
    public void ValidateCreate_AcceptsEverySupportedScopeAndMode(string mode, string scope, bool withUnit, bool withSlot)
    {
        var unitId = withUnit ? Guid.NewGuid() : (Guid?)null;
        var slotId = withSlot ? Guid.NewGuid() : (Guid?)null;

        // Lower-case input proves the rule normalises instead of rejecting shape-only differences.
        var validated = EmergencyRestrictionRules.ValidateCreate(
            mode.ToLowerInvariant(), scope.ToLowerInvariant(), unitId, slotId, "  Flooding  ",
            new DateTimeOffset(Now), null);

        Assert.Equal(mode, validated.Mode);
        Assert.Equal(scope, validated.Scope);
        Assert.Equal("Flooding", validated.Reason);
    }

    [Fact]
    public void ValidateCreate_RejectsInvalidMode()
    {
        var error = Assert.Throws<StructureException>(() => EmergencyRestrictionRules.ValidateCreate(
            "PANIC", "SITE", null, null, "reason", new DateTimeOffset(Now), null));

        Assert.Equal("INVALID_STRUCTURE", error.Code);
    }

    [Fact]
    public void ValidateCreate_RejectsBlankReason()
    {
        var error = Assert.Throws<StructureException>(() => EmergencyRestrictionRules.ValidateCreate(
            "EMERGENCY", "SITE", null, null, "   ", new DateTimeOffset(Now), null));

        Assert.Equal("INVALID_STRUCTURE", error.Code);
    }

    [Fact]
    public void ValidateCreate_RejectsNonFutureEffectiveUntil()
    {
        var error = Assert.Throws<StructureException>(() => EmergencyRestrictionRules.ValidateCreate(
            "EMERGENCY", "SITE", null, null, "reason", new DateTimeOffset(Now), new DateTimeOffset(Now)));

        Assert.Equal("INVALID_STRUCTURE", error.Code);
    }

    [Fact]
    public void ValidateCreate_RejectsSlotScopeWithoutUnit()
    {
        var error = Assert.Throws<StructureException>(() => EmergencyRestrictionRules.ValidateCreate(
            "EMERGENCY", "SLOT", null, Guid.NewGuid(), "reason", new DateTimeOffset(Now), null));

        Assert.Equal("INVALID_STRUCTURE", error.Code);
    }

    [Fact]
    public void ValidateCreate_RejectsSiteScopeCarryingAUnit()
    {
        var error = Assert.Throws<StructureException>(() => EmergencyRestrictionRules.ValidateCreate(
            "EMERGENCY", "SITE", Guid.NewGuid(), null, "reason", new DateTimeOffset(Now), null));

        Assert.Equal("INVALID_STRUCTURE", error.Code);
    }

    [Fact]
    public void ValidateCreate_RejectsUnitScopeCarryingASlot()
    {
        var error = Assert.Throws<StructureException>(() => EmergencyRestrictionRules.ValidateCreate(
            "EMERGENCY", "UNIT", Guid.NewGuid(), Guid.NewGuid(), "reason", new DateTimeOffset(Now), null));

        Assert.Equal("INVALID_STRUCTURE", error.Code);
    }

    // ------------------------------------------------------------ publisher

    [Fact]
    public async Task Publisher_PublishesRestrictedAndReleasedPayloadsVerbatim()
    {
        var endpoint = new RecordingPublishEndpoint();
        var publisher = new MassTransitFacilityCapacityRestrictionPublisher(endpoint);
        var slotId = Guid.NewGuid();
        var restricted = new FacilityCapacityRestrictedV1(Guid.NewGuid(), 1, new DateTimeOffset(Now), TenantId,
            SiteId, Guid.NewGuid(), "FORCED_MAINTENANCE", "SLOT", ZoneA, [slotId], new DateTimeOffset(Now),
            new DateTimeOffset(Now.AddHours(2)), "Burst pipe");

        await publisher.StageRestrictedAsync(restricted);

        var published = Assert.Single(endpoint.Restricted);
        Assert.Equal(restricted, published);
        Assert.Same(restricted, published);

        var released = new FacilityCapacityRestrictionReleasedV1(Guid.NewGuid(), 1, new DateTimeOffset(Now),
            TenantId, SiteId, restricted.RestrictionId, [slotId]);

        await publisher.StageReleasedAsync(released);

        var publishedRelease = Assert.Single(endpoint.Released);
        Assert.Equal(released, publishedRelease);
    }

    [Fact]
    public async Task Publisher_RejectsMessagesWithoutRequiredIdentifiers()
    {
        var publisher = new MassTransitFacilityCapacityRestrictionPublisher(new RecordingPublishEndpoint());
        var valid = new FacilityCapacityRestrictedV1(Guid.NewGuid(), 1, new DateTimeOffset(Now), TenantId,
            SiteId, Guid.NewGuid(), "EMERGENCY", "SITE", null, [], new DateTimeOffset(Now), null, "reason");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.StageRestrictedAsync(valid with { EventId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.StageRestrictedAsync(valid with { TenantId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.StageRestrictedAsync(valid with { SiteId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.StageRestrictedAsync(valid with { RestrictionId = Guid.Empty }));

        var released = new FacilityCapacityRestrictionReleasedV1(Guid.NewGuid(), 1, new DateTimeOffset(Now),
            TenantId, SiteId, Guid.NewGuid(), []);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.StageReleasedAsync(released with { RestrictionId = Guid.Empty }));
    }

    // ----------------------------------------------------------- controller

    [Fact]
    public async Task Create_Returns201WithTheCreatedRestrictionWhenOverrideIsAuthorized()
    {
        var restriction = Restriction("EMERGENCY", "SITE");
        var store = new FakeEmergencyRestrictionStore { Created = restriction };
        var controller = Controller(store, authorized: true);

        var result = await controller.CreateEmergencyRestriction(SiteId,
            new CreateEmergencyRestrictionRequest("EMERGENCY", "SITE", null, null, "Flooding", null),
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.NotNull(created.Value);
        Assert.Equal("EMERGENCY", store.LastMode);
        Assert.Equal("SITE", store.LastScope);
        Assert.Equal("Flooding", store.LastReason);
    }

    [Fact]
    public async Task Create_Returns403WhenTheOperatorLacksSiteWideOverride()
    {
        var store = new FakeEmergencyRestrictionStore();
        var controller = Controller(store, authorized: false);

        var result = await controller.CreateEmergencyRestriction(SiteId,
            new CreateEmergencyRestrictionRequest("EMERGENCY", "SITE", null, null, "Flooding", null),
            CancellationToken.None);

        var denied = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, denied.StatusCode);
        Assert.Null(store.LastMode); // the store is never reached
    }

    [Fact]
    public async Task Create_Returns400ThroughTheErrorEnvelopeForAnInvalidScope()
    {
        var store = new FakeEmergencyRestrictionStore();
        var controller = Controller(store, authorized: true);

        var error = await Assert.ThrowsAsync<StructureException>(() => controller.CreateEmergencyRestriction(SiteId,
            new CreateEmergencyRestrictionRequest("EMERGENCY", "SITE", ZoneA, null, "Flooding", null),
            CancellationToken.None));
        Assert.Equal("INVALID_STRUCTURE", error.Code);

        var status = await WriteThroughHandlerAsync(error);
        Assert.Equal(400, status.StatusCode);
        Assert.Equal("INVALID_STRUCTURE", status.Code);
    }

    [Fact]
    public async Task Create_Returns400ThroughTheErrorEnvelopeForABlankReason()
    {
        var controller = Controller(new FakeEmergencyRestrictionStore(), authorized: true);

        var error = await Assert.ThrowsAsync<StructureException>(() => controller.CreateEmergencyRestriction(SiteId,
            new CreateEmergencyRestrictionRequest("EMERGENCY", "SITE", null, null, "  ", null),
            CancellationToken.None));

        var status = await WriteThroughHandlerAsync(error);
        Assert.Equal(400, status.StatusCode);
        Assert.Equal("INVALID_STRUCTURE", status.Code);
    }

    [Fact]
    public async Task Release_Returns404WhenTheRestrictionIsUnknown()
    {
        var store = new FakeEmergencyRestrictionStore { Released = null };
        var controller = Controller(store, authorized: true);

        var result = await controller.ReleaseEmergencyRestriction(SiteId, Guid.NewGuid(), CancellationToken.None);

        var missing = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, missing.StatusCode);
    }

    [Fact]
    public async Task Release_Returns200WithTheReleasedRestriction()
    {
        var store = new FakeEmergencyRestrictionStore
        {
            Released = Restriction("FORCED_MAINTENANCE", "UNIT") with
            {
                ReleasedAt = new DateTimeOffset(Now),
                ReleasedBy = UserId
            }
        };
        var controller = Controller(store, authorized: true);

        var result = await controller.ReleaseEmergencyRestriction(SiteId, store.Released!.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(store.Released.Id, store.LastReleasedId);
    }

    [Fact]
    public async Task Release_Returns403WhenTheOperatorLacksSiteWideOverride()
    {
        var controller = Controller(new FakeEmergencyRestrictionStore(), authorized: false);

        var result = await controller.ReleaseEmergencyRestriction(SiteId, Guid.NewGuid(), CancellationToken.None);

        var denied = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, denied.StatusCode);
    }

    // -------------------------------------------------------------- overlay

    [Fact]
    public void Overlay_EmergencySiteWide_TurnsAvailableIntoUnavailableAndKeepsOccupied()
    {
        var overlay = new OperationalRestrictionOverlay(
            [Restriction("EMERGENCY", "SITE")], Now, UnitParents);
        var available = Slot(Guid.NewGuid());
        var occupied = Slot(Guid.NewGuid(), PhysicalState.OCCUPIED);

        var hidden = overlay.Apply(available, BlockInZoneA);
        Assert.Equal(PhysicalState.UNAVAILABLE, hidden.PhysicalState);
        Assert.False(hidden.IsBookable);

        // A parked vehicle is never rewritten by an emergency closure.
        var kept = overlay.Apply(occupied, BlockInZoneA);
        Assert.Equal(PhysicalState.OCCUPIED, kept.PhysicalState);
    }

    [Fact]
    public void Overlay_ForcedMaintenanceSiteWide_TurnsAvailableIntoMaintenance()
    {
        var overlay = new OperationalRestrictionOverlay(
            [Restriction("FORCED_MAINTENANCE", "SITE")], Now, UnitParents);

        var hidden = overlay.Apply(Slot(Guid.NewGuid()), BlockInZoneA);

        Assert.Equal(PhysicalState.MAINTENANCE, hidden.PhysicalState);
        Assert.False(hidden.IsBookable);
    }

    [Fact]
    public void Overlay_UnitScope_CoversDescendantsOnly()
    {
        var overlay = new OperationalRestrictionOverlay(
            [Restriction("EMERGENCY", "UNIT", unitId: ZoneA)], Now, UnitParents);

        Assert.Equal(PhysicalState.UNAVAILABLE, overlay.Apply(Slot(Guid.NewGuid()), BlockInZoneA).PhysicalState);
        Assert.Equal(PhysicalState.AVAILABLE, overlay.Apply(Slot(Guid.NewGuid()), FloorB).PhysicalState);
        Assert.True(overlay.IsBlocked(Guid.NewGuid(), BlockInZoneA));
        Assert.False(overlay.IsBlocked(Guid.NewGuid(), FloorA));
    }

    [Fact]
    public void Overlay_SlotScope_CoversOnlyThatSlot()
    {
        var target = Guid.NewGuid();
        var overlay = new OperationalRestrictionOverlay(
            [Restriction("EMERGENCY", "SLOT", unitId: ZoneA, slotId: target)], Now, UnitParents);

        Assert.Equal(PhysicalState.UNAVAILABLE, overlay.Apply(Slot(target), ZoneA).PhysicalState);
        Assert.Equal(PhysicalState.AVAILABLE, overlay.Apply(Slot(Guid.NewGuid()), ZoneA).PhysicalState);
    }

    [Fact]
    public void Overlay_ReleasedOrExpiredRestriction_CoversNothing()
    {
        var released = Restriction("EMERGENCY", "SITE") with { ReleasedAt = new DateTimeOffset(Now) };
        var expired = Restriction("EMERGENCY", "SITE") with { EffectiveUntil = new DateTimeOffset(Now.AddMinutes(-1)) };
        var overlay = new OperationalRestrictionOverlay([released, expired], Now, UnitParents);
        var slot = Slot(Guid.NewGuid());

        Assert.Equal(PhysicalState.AVAILABLE, overlay.Apply(slot, BlockInZoneA).PhysicalState);
        Assert.False(overlay.IsBlocked(slot.SlotId, BlockInZoneA));
    }

    // --------------------------------------------------------------- ticker

    [Fact]
    public async Task Ticker_RunsMaintenanceBoundaryAndEmergencyExpiryOnTheSameTick()
    {
        var maintenance = new RecordingMaintenanceWindowStore();
        var emergency = new FakeEmergencyRestrictionStore();

        await Ticker(maintenance, emergency).RunTickAsync(CancellationToken.None);

        Assert.Equal(1, maintenance.NotifyCalls);
        Assert.Equal(1, emergency.ExpireDueCalls);
    }

    [Fact]
    public async Task Ticker_ContinuesWhenTheEmergencyStoreIsNotRegistered()
    {
        var maintenance = new RecordingMaintenanceWindowStore();

        // The expiry store is optional; its absence must not stop the maintenance boundary sweep.
        await Ticker(maintenance, emergency: null).RunTickAsync(CancellationToken.None);

        Assert.Equal(1, maintenance.NotifyCalls);
    }

    [Fact]
    public async Task Ticker_StillExpiresWhenTheMaintenanceBoundaryThrows()
    {
        var maintenance = new RecordingMaintenanceWindowStore(() => throw new InvalidOperationException("boundary down"));
        var emergency = new FakeEmergencyRestrictionStore();

        // A transient failure in one sweep must not prevent the other from running on the same tick.
        await Ticker(maintenance, emergency).RunTickAsync(CancellationToken.None);

        Assert.Equal(1, maintenance.NotifyCalls);
        Assert.Equal(1, emergency.ExpireDueCalls);
    }

    [Fact]
    public async Task Ticker_SwallowsAnEmergencyExpiryFailure()
    {
        var maintenance = new RecordingMaintenanceWindowStore();
        var emergency = new FakeEmergencyRestrictionStore
        {
            OnExpireDue = () => throw new InvalidOperationException("expiry down")
        };

        // A failing expiry sweep is logged, never rethrown out of the tick.
        await Ticker(maintenance, emergency).RunTickAsync(CancellationToken.None);

        Assert.Equal(1, maintenance.NotifyCalls);
        Assert.Equal(1, emergency.ExpireDueCalls);
    }

    // -------------------------------------------------------------- helpers

    private static EmergencyRestriction Restriction(string mode, string scope,
        Guid? unitId = null, Guid? slotId = null)
        => new(Guid.NewGuid(), TenantId, SiteId, mode, scope,
            scope == EmergencyRestrictionRules.SiteScope ? null : unitId ?? ZoneA,
            scope == EmergencyRestrictionRules.SlotScope ? slotId ?? Guid.NewGuid() : null,
            [], "Flooding", new DateTimeOffset(Now), new DateTimeOffset(Now.AddHours(2)),
            UserId, new DateTimeOffset(Now));

    private static SlotVisualStateDto Slot(Guid id, PhysicalState state = PhysicalState.AVAILABLE)
        => new(id, "A-01", FloorA, "L1", null, null, VehicleType.CAR, SlotType.STANDARD,
            state, ReservationState.UNRESERVED, null, false,
            state == PhysicalState.AVAILABLE, null, null, null, DateTimeOffset.UtcNow);

    private static OperatorOperationsController Controller(FakeEmergencyRestrictionStore store, bool authorized)
        => new(new StubOperatorIdentity(), null!,
            new StubSlotAuthorizer(authorized), store);

    // Exercises the real envelope so "400" is asserted from actual output, not from the exception type.
    private static async Task<(int StatusCode, string? Code)> WriteThroughHandlerAsync(StructureException error)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var handler = new FacilityExceptionHandler(NullLogger<FacilityExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, error, CancellationToken.None));
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, document.RootElement.GetProperty("code").GetString());
    }

    private static MaintenanceBoundaryTickerWorker Ticker(
        IMaintenanceWindowStore maintenance, IEmergencyRestrictionStore? emergency)
    {
        var services = new ServiceCollection();
        services.AddSingleton(maintenance);
        if (emergency is not null) services.AddSingleton(emergency);
        var provider = services.BuildServiceProvider();
        return new MaintenanceBoundaryTickerWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MaintenanceBoundaryTickerWorker>.Instance);
    }

    private sealed class RecordingMaintenanceWindowStore : IMaintenanceWindowStore
    {
        private readonly Func<Task>? _onNotify;

        public RecordingMaintenanceWindowStore(Func<Task>? onNotify = null) => _onNotify = onNotify;

        public int NotifyCalls { get; private set; }

        public async Task<int> NotifyDueBoundariesAsync(DateTime now, CancellationToken ct)
        {
            NotifyCalls++;
            if (_onNotify is not null) await _onNotify();
            return 0;
        }

        public Task<IReadOnlyList<MaintenanceWindow>> ListAsync(OwnerScope owner, Guid siteId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<MaintenanceWindow?> GetByIdAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<MaintenanceWindow> CreateAsync(OwnerScope owner, Guid siteId, DateTime start, DateTime end,
            string reason, CancellationToken ct, Guid? unitId = null, Guid? slotId = null)
            => throw new NotSupportedException();

        public Task<MaintenanceWindow> ExtendAsync(OwnerScope owner, Guid siteId, Guid windowId, DateTime newEnd,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class FakeEmergencyRestrictionStore : IEmergencyRestrictionStore
    {
        public EmergencyRestriction? Created { get; init; }
        public EmergencyRestriction? Released { get; init; }
        public string? LastMode { get; private set; }
        public string? LastScope { get; private set; }
        public string? LastReason { get; private set; }
        public Guid? LastReleasedId { get; private set; }
        public List<EmergencyRestriction> Active { get; } = [];

        public Task<IReadOnlyList<EmergencyRestriction>> ListActiveAsync(Guid tenantId, Guid siteId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EmergencyRestriction>>(Active);

        public Task<EmergencyRestriction> CreateAsync(Guid tenantId, Guid siteId, Guid actorId, string actorRole,
            string mode, string scope, Guid? unitId, Guid? slotId, string reason, DateTimeOffset? effectiveUntil,
            CancellationToken ct)
        {
            LastMode = mode;
            LastScope = scope;
            LastReason = reason;
            return Task.FromResult(Created ?? throw new InvalidOperationException("No created restriction configured."));
        }

        public Task<EmergencyRestriction?> ReleaseAsync(Guid tenantId, Guid siteId, Guid restrictionId,
            Guid actorId, string actorRole, CancellationToken ct)
        {
            LastReleasedId = restrictionId;
            return Task.FromResult(Released);
        }

        public int ExpireDueCalls { get; private set; }
        public Func<Task<int>>? OnExpireDue { get; init; }

        public Task<int> ExpireDueAsync(DateTimeOffset now, CancellationToken ct)
        {
            ExpireDueCalls++;
            return OnExpireDue?.Invoke() ?? Task.FromResult(0);
        }
    }

    private sealed class StubOperatorIdentity : IOperatorSiteAuthorizer
    {
        public Task<OwnerScope> ScopeAsync(Guid siteId, CancellationToken ct)
            => Task.FromResult(new OwnerScope(UserId, TenantId));
    }

    private sealed class StubSlotAuthorizer(bool authorized) : ISlotOperatorAuthorizer
    {
        public Task<OwnerScope> ScopeAsync(Guid site, CancellationToken cancellationToken)
            => Task.FromResult(new OwnerScope(UserId, TenantId));

        public Task<bool> IsAuthorizedAsync(OwnerScope actor, Guid site, CancellationToken cancellationToken)
            => Task.FromResult(authorized);
    }
}