using Microsoft.EntityFrameworkCore;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Persistence;

namespace SmartParking.FacilityService.Tests;

// SPARK-244 G5 coverage for the facility-owned projection ledger. These are integration tests: the
// release-once SQL only behaves correctly against real PostgreSQL constraints (ON CONFLICT on the
// unique ledger index, FOR UPDATE watermarks), so they need SMARTPARK_STRUCTURE_TEST_CONNECTION
// pointing at an isolated server with CREATE DATABASE. Until that variable is set they are skipped
// through the shared [StructurePostgresFact] attribute (declared in PostgresStructureWorkflowTests).
public sealed class OccupancyProjectionRepositoryTests
{
    [StructurePostgresFact]
    public async Task DuplicateEventIdIsAppliedOnce()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();

        var eventId = Guid.NewGuid();
        var change = new OccupancyProjectionEvent(
            EventId: eventId,
            EventType: "SlotStateChanged",
            OccurredAt: DateTimeOffset.UtcNow,
            SequenceNumber: 1,
            SiteId: fixture.SiteId,
            SlotId: fixture.SlotId,
            SessionId: null,
            PhysicalState: "OCCUPIED");

        // First delivery projects the change.
        Assert.True(await fixture.ProjectAsync(change, CancellationToken.None));
        // Replaying the same eventId rolls back before any projection runs and reports false.
        Assert.False(await fixture.ProjectAsync(change, CancellationToken.None));

        Assert.Equal(1L, await fixture.ProcessedEventCountAsync(eventId));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
    }

    [StructurePostgresFact]
    public async Task SpentReleaseLedgerDoesNotFreeASlotReoccupiedByAnotherSession()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();

        var sessionA = Guid.NewGuid();

        // Session A occupies the slot, then releases it: the ledger records session A once.
        Assert.True(await fixture.ProjectAsync(Change(fixture, "OCCUPIED", 1), CancellationToken.None));
        Assert.True(await fixture.ProjectAsync(Release(fixture, sessionA, sequenceNumber: 2), CancellationToken.None));
        Assert.Equal("AVAILABLE", await fixture.SlotStateAsync());
        Assert.Equal(1L, await fixture.ReleaseLedgerSessionCountAsync(sessionA));

        // A new session B occupies the same slot in a later sequence.
        Assert.True(await fixture.ProjectAsync(Change(fixture, "OCCUPIED", 3), CancellationToken.None));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());

        // The release advisory is redelivered for the already-spent session A. NOTE: the store
        // returns true here because the event is fresh and does advance the watermark; the
        // release-once guarantee is therefore asserted on database state, not on this return value.
        Assert.True(await fixture.ProjectAsync(Release(fixture, sessionA, sequenceNumber: 4), CancellationToken.None));

        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
        Assert.Equal(1L, await fixture.ReleaseLedgerSessionCountAsync(sessionA));
    }

    [StructurePostgresFact]
    public async Task ReleaseWithoutSlotIsRecordedButNotProjected()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();

        var session = Guid.NewGuid();

        Assert.True(await fixture.ProjectAsync(Change(fixture, "OCCUPIED", 1), CancellationToken.None));

        // Walk-in occupancy: no slot to project onto. The event is recorded and committed, then
        // reported as not applied, and no ledger row is written.
        var releaseEventId = Guid.NewGuid();
        var walkInRelease = new OccupancyProjectionEvent(
            EventId: releaseEventId,
            EventType: "OccupancyReleased",
            OccurredAt: DateTimeOffset.UtcNow,
            SequenceNumber: 2,
            SiteId: fixture.SiteId,
            SlotId: null,
            SessionId: session,
            PhysicalState: null);

        Assert.False(await fixture.ProjectAsync(walkInRelease, CancellationToken.None));

        Assert.Equal(1L, await fixture.ProcessedEventCountAsync(releaseEventId));
        Assert.Equal(0L, await fixture.ScalarLongAsync("SELECT count(*) FROM occupancy_release_ledger"));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
    }

    [StructurePostgresFact]
    public async Task StaleSequenceIsIgnoredAfterTheWatermarkAdvances()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();


        Assert.True(await fixture.ProjectAsync(Change(fixture, "OCCUPIED", 10), CancellationToken.None));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());

        // A late redelivery with a lower sequence for the same slot must not regress the projection.
        Assert.False(await fixture.ProjectAsync(Change(fixture, "MAINTENANCE", 5), CancellationToken.None));

        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
        Assert.Equal(10L, await fixture.WatermarkAsync());
    }

    // SPARK-244 reservation safety: the realtime projection owns only physical_state. A reservation
    // flow may have flagged the slot (BACKUP/RESERVED/PROTECTED) and neither a SlotStateChanged
    // projection nor an OccupancyReleased projection may clear that reservation flag to NULL.
    [StructurePostgresFact, Trait("Category", "Postgres")]
    public async Task SlotStateAndReleaseChangesPreserveExistingReservationState()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();

        var session = Guid.NewGuid();

        await fixture.SetReservationStateAsync("BACKUP");
        Assert.Equal("BACKUP", await fixture.ReservationStateAsync());

        // SlotStateChanged (-> OCCUPIED) must not erase the reservation flag.
        Assert.True(await fixture.ProjectAsync(Change(fixture, "OCCUPIED", 1), CancellationToken.None));
        Assert.Equal("OCCUPIED", await fixture.SlotStateAsync());
        Assert.Equal("BACKUP", await fixture.ReservationStateAsync());

        // OccupancyReleased (-> AVAILABLE) must not erase the reservation flag either.
        Assert.True(await fixture.ProjectAsync(Release(fixture, session, sequenceNumber: 2), CancellationToken.None));
        Assert.Equal("AVAILABLE", await fixture.SlotStateAsync());
        Assert.Equal("BACKUP", await fixture.ReservationStateAsync());
    }

    [StructurePostgresFact]
    public async Task StructureSiteLockFencesProjectionAndRedeliveryRetainsItsOriginalIdentity()
    {
        await using var fixture = await OccupancyProjectionFixture.CreateAsync();
        var change = Change(fixture, "MAINTENANCE", 1);
        await fixture.ExecuteAsync($"UPDATE parking_slots SET supported_vehicle_type='CAR' WHERE id='{fixture.SlotId}'");
        var options = new DbContextOptionsBuilder<ParkingDbContext>().UseNpgsql(fixture.DataSource).Options;
        await using var owner = new ParkingDbContext(options);
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await owner.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({fixture.SiteId.ToString()},2))");
        var conflict = await Assert.ThrowsAsync<SmartParking.FacilityService.Domain.StructureException>(() =>
            fixture.ProjectAsync(change, default));
        Assert.Equal("STRUCTURE_BUSY", conflict.Code);
        Assert.Equal(0L, await fixture.ProcessedEventCountAsync(change.EventId));
        Assert.Equal("AVAILABLE", await fixture.SlotStateAsync());
        await transaction.RollbackAsync();
        Assert.True(await fixture.ProjectAsync(change, default));
        Assert.False(await fixture.ProjectAsync(change, default));
        Assert.Equal("MAINTENANCE", await fixture.SlotStateAsync());

    }

    private static OccupancyProjectionEvent Change(OccupancyProjectionFixture fixture, string physicalState, long sequenceNumber)
        => new(Guid.NewGuid(), "SlotStateChanged", DateTimeOffset.UtcNow, sequenceNumber,
            fixture.SiteId, fixture.SlotId, null, physicalState);

    private static OccupancyProjectionEvent Release(OccupancyProjectionFixture fixture, Guid sessionId, long sequenceNumber)
        => new(Guid.NewGuid(), "OccupancyReleased", DateTimeOffset.UtcNow, sequenceNumber,
            fixture.SiteId, fixture.SlotId, sessionId, null);
}
