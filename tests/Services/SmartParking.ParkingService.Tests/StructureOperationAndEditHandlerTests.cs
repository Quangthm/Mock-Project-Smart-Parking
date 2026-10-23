using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Tests;

public sealed class StructureOperationAndEditHandlerTests
{
    private static ParkingStructure CreateSampleStructure()
    {
        var site = new ParkingStructure(new(Guid.NewGuid(), Guid.NewGuid(), "TEST_SITE", "Test Site", "123 Main St", 10.0m, 20.0m, true, 0));
        var zone = site.AddUnit(UnitType.ZONE, "Zone 1", maxCapacity: 5);
        site.AddSlot(zone, "A01", VehicleType.CAR);
        return site;
    }

    [Fact]
    public void StructureEditHandler_AppliesProfileUpdate()
    {
        var structure = CreateSampleStructure();
        var edit = new StructureEdit("profile", Code: "NEW_CODE", Name: "New Name", Address: "456 Oak St", Latitude: 11.5m, Longitude: 21.5m);

        StructureEditHandler.Apply(structure, edit);

        Assert.Equal("NEW_CODE", structure.Site.Code);
        Assert.Equal("New Name", structure.Site.Name);
        Assert.Equal("456 Oak St", structure.Site.Address);
    }

    [Fact]
    public void StructureEditHandler_AppliesActiveToggle()
    {
        var structure = CreateSampleStructure();
        var edit = new StructureEdit("active", Active: false);

        StructureEditHandler.Apply(structure, edit);

        Assert.False(structure.Site.IsActive);
    }

    [Fact]
    public void StructureEditHandler_AppliesRenameUnitAndCapacity()
    {
        var structure = CreateSampleStructure();
        var unitId = structure.Units[0].Id;

        StructureEditHandler.Apply(structure, new StructureEdit("renameUnit", ResourceId: unitId, Name: "Renamed Zone"));
        Assert.Equal("Renamed Zone", structure.Units[0].Name);

        StructureEditHandler.Apply(structure, new StructureEdit("capacity", ResourceId: unitId, Capacity: 10));
        Assert.Equal(10, structure.Units[0].MaxCapacity);
    }

    [Fact]
    public void StructureEditHandler_AppliesMoveAndConfigureSlot()
    {
        var structure = CreateSampleStructure();
        var zone2 = structure.AddUnit(UnitType.ZONE, "Zone 2", maxCapacity: 5);
        var slotId = structure.Slots[0].Id;

        StructureEditHandler.Apply(structure, new StructureEdit("moveSlot", ResourceId: slotId, UnitId: zone2, Code: "B01"));
        Assert.Equal("B01", structure.Slots[0].Code);
        Assert.Equal(zone2, structure.Slots[0].UnitId);

        StructureEditHandler.Apply(structure, new StructureEdit("configureSlot", ResourceId: slotId, VehicleType: VehicleType.MOTORCYCLE, SlotType: SlotType.STANDARD, Coordinates3D: "{\"x\":1}", Features: "{\"ev\":true}"));
        Assert.Equal(VehicleType.MOTORCYCLE, structure.Slots[0].VehicleType);
        Assert.Equal("{\"x\":1}", structure.Slots[0].Coordinates3D);
    }

    [Fact]
    public void StructureEditHandler_AppliesBackupAndMarkBackup()
    {
        var structure = CreateSampleStructure();
        var slotId = structure.Slots[0].Id;

        StructureEditHandler.Apply(structure, new StructureEdit("backup", Capacity: 1, VehicleType: VehicleType.CAR));
        Assert.NotEmpty(structure.BackupPolicies);

        StructureEditHandler.Apply(structure, new StructureEdit("markBackup", ResourceId: slotId, Active: true));
        Assert.Equal("BACKUP", structure.Slots[0].ReservationState);
    }

    [Fact]
    public void StructureEditHandler_AppliesRemoveSlotAndRemoveUnit()
    {
        var structure = CreateSampleStructure();
        var slotId = structure.Slots[0].Id;
        var unitId = structure.Units[0].Id;

        StructureEditHandler.Apply(structure, new StructureEdit("removeSlot", ResourceId: slotId));
        Assert.Empty(structure.Slots);

        StructureEditHandler.Apply(structure, new StructureEdit("removeUnit", ResourceId: unitId));
        Assert.Empty(structure.Units);
    }

    [Fact]
    public void StructureEditHandler_AppliesRemovePath()
    {
        var structure = CreateSampleStructure();
        var zone2 = structure.AddUnit(UnitType.ZONE, "Zone 2", maxCapacity: 5);
        var pathId = structure.AddAccessPath("PATH_1", structure.Units[0].Id, zone2);

        StructureEditHandler.Apply(structure, new StructureEdit("removePath", ResourceId: pathId));
        Assert.Empty(structure.Paths);
    }

    [Fact]
    public void StructureEditHandler_ThrowsOnUnsupportedAction()
    {
        var structure = CreateSampleStructure();
        var ex = Assert.Throws<StructureException>(() =>
            StructureEditHandler.Apply(structure, new StructureEdit("invalid_action")));
        Assert.Equal("INVALID_STRUCTURE", ex.Code);
    }

    [Fact]
    public async Task StructureOperationService_ThrowsOnEmptyKeyOrUnsupportedAction()
    {
        var owner = new OwnerScope(Guid.NewGuid(), Guid.NewGuid());
        var siteId = Guid.NewGuid();
        var authorizer = new FakeAuthorizer();
        var store = new FakeOperationStore();
        var planner = new FakePlanner();
        var service = new StructureOperationService(store, planner, null!, authorizer);

        await Assert.ThrowsAsync<StructureException>(() =>
            service.ExecuteAsync(owner, siteId, Guid.Empty, new StructureEdit("removeSlot"), CancellationToken.None));

        await Assert.ThrowsAsync<StructureException>(() =>
            service.ExecuteAsync(owner, siteId, Guid.NewGuid(), new StructureEdit("unknownAction"), CancellationToken.None));
    }

    [Fact]
    public async Task StructureOperationService_ReadAsync_AuthorizesAndReturns()
    {
        var owner = new OwnerScope(Guid.NewGuid(), Guid.NewGuid());
        var siteId = Guid.NewGuid();
        var opId = Guid.NewGuid();
        var authorizer = new FakeAuthorizer();
        var store = new FakeOperationStore { Existing = new StructureOperation(opId, siteId, "completed", null, null) };
        var service = new StructureOperationService(store, new FakePlanner(), null!, authorizer);

        var result = await service.ReadAsync(owner, siteId, opId, CancellationToken.None);
        Assert.Equal(opId, result.Id);
        Assert.True(authorizer.WasAuthorized);
    }

    private sealed class FakeAuthorizer : IOwnerAuthorizer
    {
        public bool WasAuthorized { get; private set; }
        public Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken cancellationToken)
        {
            WasAuthorized = true;
            return Task.FromResult(true);
        }
    }

    private sealed class FakePlanner : IStructureImpactPlanner
    {
        public Task<ReallocationPlan> PlanAsync(OwnerScope owner, Guid siteId, Guid operationId, StructureEdit edit, CancellationToken cancellationToken)
            => Task.FromResult(new ReallocationPlan("success", null, "none"));
    }

    private sealed class FakeOperationStore : IStructureOperationStore
    {
        public StructureOperation? Existing { get; set; }
        public Task<IAsyncDisposable> LockAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<IAsyncDisposable>(new DummyLock());
        public Task<StructureOperation> BeginAsync(OwnerScope owner, Guid site, Guid key, StructureEdit edit, CancellationToken cancellationToken)
            => Task.FromResult(Existing ?? new StructureOperation(key, site, "pending", null, null));
        public Task<StructureOperation> UpdateAsync(OwnerScope owner, Guid id, string status, string? reason, string? impact, CancellationToken cancellationToken)
            => Task.FromResult(new StructureOperation(id, Guid.NewGuid(), status, reason, impact));
        public Task<StructureOperation> ReadAsync(OwnerScope owner, Guid site, Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Existing ?? new StructureOperation(id, site, "completed", null, null));

        private sealed class DummyLock : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
