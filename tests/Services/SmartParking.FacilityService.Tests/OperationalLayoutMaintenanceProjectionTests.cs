using SmartParking.FacilityService.Application.Common.Helpers;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Tests;

// Operator projection overlay (DE-3/DE-13): only a live, acknowledged window may hide a slot,
// its scope decides which slots it covers, and a parked vehicle is never rewritten.
public sealed class OperationalLayoutMaintenanceProjectionTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowStart = Now.AddMinutes(-30);
    private static readonly DateTime WindowEnd = Now.AddHours(2);

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

    private static MaintenanceWindow Window(string enforcement = "acknowledged",
        Guid? unitId = null, Guid? slotId = null, DateTime? cancelled = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), unitId, slotId, WindowStart, WindowEnd,
            "Repaving", Guid.NewGuid(), WindowStart.AddDays(-1), cancelled, enforcement,
            enforcement == "acknowledged" ? Guid.NewGuid() : null,
            enforcement == "acknowledged" ? WindowStart.AddDays(-1) : null);

    private static OperationalMaintenanceOverlay Overlay(params MaintenanceWindow[] windows)
        => new(windows, "ACTIVE", Now, UnitParents);

    private static SlotVisualStateDto Slot(Guid id, PhysicalState state = PhysicalState.AVAILABLE)
        => new(id, "A-01", FloorA, "L1", null, null, VehicleType.CAR, SlotType.STANDARD,
            state, ReservationState.UNRESERVED, null, false,
            state == PhysicalState.AVAILABLE, null, null, null, DateTimeOffset.UtcNow);

    [Fact]
    public void NoWindows_LeavesServiceAvailabilityNull()
    {
        var overlay = Overlay();

        Assert.Null(overlay.ServiceAvailability);
        // The v1.2.0 wire shape is preserved: no maintenance block is emitted at all.
        Assert.False(overlay.IsBlocked(Guid.NewGuid(), BlockInZoneA));
    }

    [Fact]
    public void AcknowledgedSiteWideWindow_HidesAvailableSlotsButNeverAnOccupiedOne()
    {
        var overlay = Overlay(Window());
        var available = Slot(Guid.NewGuid());
        var occupied = Slot(Guid.NewGuid(), PhysicalState.OCCUPIED);

        var hidden = overlay.Apply(available, BlockInZoneA);
        Assert.Equal(PhysicalState.MAINTENANCE, hidden.PhysicalState);
        Assert.False(hidden.IsBookable);

        var kept = overlay.Apply(occupied, BlockInZoneA);
        Assert.Equal(PhysicalState.OCCUPIED, kept.PhysicalState);

        var availability = overlay.ServiceAvailability!;
        Assert.Equal(MaintenanceWindowState.Maintenance, availability.State);
        Assert.Equal(new DateTimeOffset(WindowEnd), availability.ClosedUntil);
        Assert.Equal("Repaving", availability.Reason);
        var active = Assert.Single(availability.ActiveWindows);
        Assert.Equal("acknowledged", active.EnforcementState);
        Assert.Null(active.UnitId);
        Assert.Null(active.SlotId);
    }

    [Fact]
    public void PendingWindow_IsUncertainAndLeavesSlotsAvailable()
    {
        var overlay = Overlay(Window(enforcement: "pending"));

        Assert.Equal(MaintenanceWindowState.MaintenanceUncertain, overlay.ServiceAvailability!.State);

        var slot = Slot(Guid.NewGuid());
        var result = overlay.Apply(slot, BlockInZoneA);
        Assert.Equal(PhysicalState.AVAILABLE, result.PhysicalState);
        Assert.True(result.IsBookable);
    }

    [Fact]
    public void UnitScopedWindow_CoversOnlyItsOwnSubtree()
    {
        var overlay = Overlay(Window(unitId: ZoneA));

        var inZone = Slot(Guid.NewGuid());
        Assert.Equal(PhysicalState.MAINTENANCE, overlay.Apply(inZone, BlockInZoneA).PhysicalState);

        var otherFloor = Slot(Guid.NewGuid());
        var untouched = overlay.Apply(otherFloor, FloorB);
        Assert.Equal(PhysicalState.AVAILABLE, untouched.PhysicalState);
        Assert.True(untouched.IsBookable);
    }

    [Fact]
    public void SlotScopedWindow_CoversOnlyThatSlot()
    {
        var target = Guid.NewGuid();
        var sibling = Guid.NewGuid();
        var overlay = Overlay(Window(unitId: ZoneA, slotId: target));

        Assert.Equal(PhysicalState.MAINTENANCE, overlay.Apply(Slot(target), BlockInZoneA).PhysicalState);

        var siblingSlot = overlay.Apply(Slot(sibling), BlockInZoneA);
        Assert.Equal(PhysicalState.AVAILABLE, siblingSlot.PhysicalState);
        Assert.True(siblingSlot.IsBookable);
    }

    [Fact]
    public void CancelledOrNotYetStartedWindow_DoesNotHideSlots()
    {
        var cancelled = Overlay(Window(cancelled: Now.AddMinutes(-5)));
        Assert.False(cancelled.IsBlocked(Guid.NewGuid(), BlockInZoneA));
        Assert.Equal(MaintenanceWindowState.Active, cancelled.ServiceAvailability!.State);

        var future = Overlay(Window() with { Start = Now.AddHours(1), End = Now.AddHours(2) });
        Assert.False(future.IsBlocked(Guid.NewGuid(), BlockInZoneA));
        Assert.Equal(MaintenanceWindowState.Scheduled, future.ServiceAvailability!.State);
    }
}