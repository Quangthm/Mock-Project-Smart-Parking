using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Tests;

public sealed class MaintenanceWindowTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static MaintenanceWindow Window(DateTime start, DateTime end, string enforcement = "pending", DateTime? cancelled = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, start, end, "Repaving",
            Guid.NewGuid(), start.AddDays(-1), cancelled, enforcement, null, null);

    [Fact]
    public void HalfOpenBoundaryIsInsideAtStartAndOutsideAtEnd()
    {
        var window = Window(Now, Now.AddHours(2));

        Assert.True(MaintenanceWindowState.Covers(window, Now));                    // now == start is inside
        Assert.True(MaintenanceWindowState.Covers(window, Now.AddHours(2).AddTicks(-1)));
        Assert.False(MaintenanceWindowState.Covers(window, Now.AddHours(2)));       // now == end is outside
        Assert.Equal(MaintenanceWindowState.MaintenanceUncertain, MaintenanceWindowState.Derive([window], "ACTIVE", Now));
        // At `end` it no longer covers now, and it started in the past: serving again, not scheduled.
        Assert.Equal(MaintenanceWindowState.Active, MaintenanceWindowState.Derive([window], "ACTIVE", Now.AddHours(2)));
        Assert.Null(MaintenanceWindowState.ClosedUntil([window], Now.AddHours(2)));
        Assert.Equal(Now.AddHours(2), MaintenanceWindowState.ClosedUntil([window], Now));
    }

    [Fact]
    public void PendingAndFailedAreUncertainWhileAcknowledgedIsMaintenance()
    {
        Assert.Equal(MaintenanceWindowState.Maintenance,
            MaintenanceWindowState.Derive([Window(Now, Now.AddHours(1), "acknowledged")], "ACTIVE", Now));
        Assert.Equal(MaintenanceWindowState.MaintenanceUncertain,
            MaintenanceWindowState.Derive([Window(Now, Now.AddHours(1), "pending")], "ACTIVE", Now));
        Assert.Equal(MaintenanceWindowState.MaintenanceUncertain,
            MaintenanceWindowState.Derive([Window(Now, Now.AddHours(1), "failed")], "ACTIVE", Now));
    }

    [Fact]
    public void FutureAndCancelledWindowsDeriveScheduledActiveAndInactive()
    {
        Assert.Equal(MaintenanceWindowState.Scheduled,
            MaintenanceWindowState.Derive([Window(Now.AddHours(1), Now.AddHours(2))], "ACTIVE", Now));
        Assert.Equal(MaintenanceWindowState.Active,
            MaintenanceWindowState.Derive([Window(Now, Now.AddHours(2), cancelled: Now.AddMinutes(-1))], "ACTIVE", Now));
        Assert.Equal(MaintenanceWindowState.Inactive,
            MaintenanceWindowState.Derive([Window(Now, Now.AddHours(2), "acknowledged")], "INACTIVE", Now));
    }

    [Fact]
    public void ScopedWindowsAreRejectedAtTheBoundary()
    {
        Error("INVALID_STRUCTURE", () => MaintenanceWindowRules.ValidateCreate(
            Guid.NewGuid(), null, "Repaving", Now.AddHours(1), Now.AddHours(2), Now));
        Error("INVALID_STRUCTURE", () => MaintenanceWindowRules.ValidateCreate(
            null, Guid.NewGuid(), "Repaving", Now.AddHours(1), Now.AddHours(2), Now));
        // A site-wide window is accepted.
        MaintenanceWindowRules.ValidateCreate(null, null, "Repaving", Now.AddHours(1), Now.AddHours(2), Now);
    }

    [Fact]
    public void CreateRejectsPastOrEmptyWindowsAndBlankReasons()
    {
        Error("INVALID_STRUCTURE", () => MaintenanceWindowRules.ValidateCreate(
            null, null, "Repaving", Now.AddHours(2), Now.AddHours(1), Now));
        Error("INVALID_STRUCTURE", () => MaintenanceWindowRules.ValidateCreate(
            null, null, "Repaving", Now.AddMinutes(-1), Now.AddHours(1), Now));
        Error("INVALID_STRUCTURE", () => MaintenanceWindowRules.ValidateCreate(
            null, null, "  ", Now.AddHours(1), Now.AddHours(2), Now));
    }

    [Fact]
    public void ExtensionOnlyGrowsWhileTheWindowHasNotEnded()
    {
        var start = Now.AddHours(1); var end = Now.AddHours(2);

        Assert.Equal(Now.AddHours(3), MaintenanceWindowRules.Extend(start, end, null, Now.AddHours(3), Now));
        Error("STRUCTURE_IN_USE", () => MaintenanceWindowRules.Extend(start, end, null, Now.AddMinutes(90), Now)); // shortening
        Error("STRUCTURE_IN_USE", () => MaintenanceWindowRules.Extend(start, end, null, end, Now));                 // no growth
        Error("STRUCTURE_IN_USE", () => MaintenanceWindowRules.Extend(start, end, start.AddHours(-1), Now.AddHours(3), Now)); // moves start
        Error("STRUCTURE_IN_USE", () => MaintenanceWindowRules.Extend(start, end, null, Now.AddHours(3), end));     // already ended
        Error("INVALID_STRUCTURE", () => MaintenanceWindowRules.Extend(start, end, null, null, Now));               // no new end
    }

    private static void Error(string code, Action action)
        => Assert.Equal(code, Assert.Throws<StructureException>(action).Code);
}
