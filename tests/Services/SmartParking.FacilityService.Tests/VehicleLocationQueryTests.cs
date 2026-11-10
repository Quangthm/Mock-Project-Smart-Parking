using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Usecase.ParkingSessions.Queries.GetCurrentVehicleLocation;

namespace SmartParking.FacilityService.Tests;

public sealed class VehicleLocationQueryTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid SlotId = Guid.NewGuid();
    private static readonly Guid ZoneId = Guid.NewGuid();

    private static ActiveVehicleLocation Fact(
        Guid? currentSlotId = null,
        Guid? currentSpatialUnitId = null,
        string? allocationStatus = "OCCUPIED",
        string? slotCode = null,
        string? zoneCode = null,
        string? floorCode = null,
        DateTimeOffset? recordedAt = null) =>
        new(SessionId, SiteId, "SITE-001", currentSlotId, currentSpatialUnitId, allocationStatus,
            slotCode, null, zoneCode, floorCode, recordedAt);

    private static async Task<VehicleLocationResponseDto?> HandleAsync(ActiveVehicleLocation? fact)
    {
        var handler = new GetCurrentVehicleLocationQueryHandler(new FakeVehicleLocationReadStore(fact));
        return await handler.Handle(new GetCurrentVehicleLocationQuery(Guid.NewGuid()), CancellationToken.None);
    }

    [Fact]
    public async Task SlotPrecision_ReturnsSlotLocationAndRecordedAt()
    {
        var recordedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        var fact = Fact(currentSlotId: SlotId, slotCode: "A-01", recordedAt: recordedAt);

        var response = await HandleAsync(fact);

        Assert.NotNull(response);
        Assert.Equal(SessionId, response.SessionId);
        Assert.Equal(SiteId, response.SiteId);
        Assert.Equal("SITE-001", response.SiteCode);
        Assert.Equal("SLOT", response.RecordedLocation.Precision);
        Assert.Equal(SlotId, response.RecordedLocation.SlotId);
        Assert.Equal("A-01", response.RecordedLocation.SlotCode);
        Assert.NotNull(response.RecordedAt);
        Assert.Equal(recordedAt, response.RecordedAt);
        Assert.False(response.Freshness.IsStale);
        Assert.Equal(30, response.Freshness.StalenessThresholdSeconds);
        Assert.Equal("OPERATIONS", response.Freshness.Source);
    }

    [Fact]
    public async Task ZonePrecision_ReturnsZoneLocationWithoutSlot()
    {
        var recordedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var fact = Fact(
            currentSpatialUnitId: ZoneId,
            slotCode: "SHOULD-BE-IGNORED",
            recordedAt: recordedAt);

        var response = await HandleAsync(fact);

        Assert.NotNull(response);
        Assert.Equal("ZONE", response.RecordedLocation.Precision);
        Assert.Null(response.RecordedLocation.SlotId);
        Assert.Null(response.RecordedLocation.SlotCode);
        Assert.Equal(ZoneId, response.RecordedLocation.ZoneId);
        Assert.Equal(recordedAt, response.RecordedAt);
    }

    [Fact]
    public async Task UnknownPrecision_ReturnsNoLocationAndNoRecordedAt()
    {
        var recordedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var fact = Fact(recordedAt: recordedAt);

        var response = await HandleAsync(fact);

        Assert.NotNull(response);
        Assert.Equal("UNKNOWN", response.RecordedLocation.Precision);
        Assert.Null(response.RecordedLocation.SlotId);
        Assert.Null(response.RecordedLocation.SlotCode);
        Assert.Null(response.RecordedLocation.ZoneId);
        Assert.Null(response.RecordedLocation.ZoneCode);
        Assert.Null(response.RecordedLocation.FloorCode);
        Assert.Null(response.RecordedAt);
    }

    [Fact]
    public async Task NoActiveSession_ReturnsNull()
    {
        var response = await HandleAsync(null);

        Assert.Null(response);
    }

    [Fact]
    public async Task ReleasedAllocationStatus_IsNotPassedThrough()
    {
        var fact = Fact(currentSlotId: SlotId, allocationStatus: "RELEASED");

        var response = await HandleAsync(fact);

        Assert.NotNull(response);
        Assert.Null(response.AllocationStatus);
    }

    private sealed class FakeVehicleLocationReadStore(ActiveVehicleLocation? result) : IVehicleLocationReadStore
    {
        public Task<ActiveVehicleLocation?> FindActiveForAccountAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult(result);
    }
}