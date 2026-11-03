using Npgsql;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Persistence;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Domain.Entities;
using OperationApplication = SmartParking.OperationService.Application;
using OperationDomain = SmartParking.OperationService.Domain;

namespace SmartParking.FacilityService.Tests;

public sealed class StructurePostgresFactAttribute : FactAttribute
{
    public StructurePostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION")))
            Skip = "Set SMARTPARK_STRUCTURE_TEST_CONNECTION to an isolated PostgreSQL server with CREATE DATABASE permission.";
    }
}

public sealed class PostgresStructureWorkflowTests
{
    [Fact]
    public void ReservationEfModelMapsExistingOperationsTablesAndKeys()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options;
        using var context = new ReservationDbContext(options);
        var model = context.Model;

        Assert.Equal("parking_sessions", model.FindEntityType(typeof(ParkingSessionRow))!.GetTableName());
        Assert.Equal("reservations", model.FindEntityType(typeof(ReservationRow))!.GetTableName());
        Assert.Equal("structure_edit_holds", model.FindEntityType(typeof(StructureEditHoldRow))!.GetTableName());
        Assert.All(model.GetEntityTypes(), entity => Assert.NotNull(entity.FindPrimaryKey()));
    }

    [StructurePostgresFact]
    public async Task CommittedEditSurvivesLostResponseAndRecoversWithANewApplicationInstance()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var slotId = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01", VehicleType.CAR);
        await fixture.ExecuteAsync(fixture.Reservation, $"""
            INSERT INTO site_capacity_pools(tenant_id,site_id,spatial_unit_id,vehicle_type,total_capacity,max_reservation_quota)
            VALUES ('{fixture.Owner.TenantId}','{fixture.SiteId}',NULL,'CAR',1,1),
                   ('{fixture.Owner.TenantId}','{fixture.SiteId}','{fixture.UnitId}','CAR',1,1)
            """);
        fixture.Transport.FailAfterRelease = true;

        await Assert.ThrowsAsync<StructureException>(() => fixture.Service.ChangeAsync(
            fixture.Owner, fixture.SiteId, structure => structure.RemoveSlot(slotId)));

        var pending = Assert.Single(await fixture.Store.PendingAsync(fixture.SiteId, CancellationToken.None));
        Assert.Equal("ready", pending.State);
        Assert.Equal(new[] { slotId }, pending.Outcome!.RemovedSlots);

        // Recreate both store and Application service as on a process restart.
        var restartedStore = new PostgresStructureSynchronizationStore(fixture.Parking, fixture.Gates);
        var restarted = new StructureSynchronizationService(restartedStore, fixture.Transport);
        Assert.Empty(await restarted.RecoverPendingAsync(CancellationToken.None));
        Assert.Empty(await restartedStore.PendingAsync(fixture.SiteId, CancellationToken.None));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            "SELECT count(*) FROM parking_slots WHERE deleted_at IS NOT NULL"));
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT count(*) FROM structure_edit_holds"));
        Assert.Equal(2L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT count(*) FROM site_capacity_pools WHERE total_capacity=0 AND max_reservation_quota=0 AND backup_count=0"));
        // The tombstone table no longer exists; removal must not depend on writing to it.
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT CASE WHEN to_regclass('structure_removed_resources') IS NULL THEN 1 ELSE 0 END"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_release_receipts WHERE token='{pending.Token}'"));

    }

    [StructurePostgresFact]
    public async Task ReservationTriggerBlocksCapacityWritesUntilTheHoldCompletes()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var token = Guid.NewGuid();
        await fixture.ReservationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, token, CancellationToken.None);

        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(fixture.Reservation, $"""
            INSERT INTO site_capacity_pools(tenant_id,site_id,vehicle_type,total_capacity,max_reservation_quota)
            VALUES ('{fixture.Owner.TenantId}','{fixture.SiteId}','CAR',1,1)
            """));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);

        await fixture.ReservationService.CompleteAsync(
            fixture.Owner.TenantId, fixture.SiteId, token, null, CancellationToken.None);
        await fixture.ExecuteAsync(fixture.Reservation, $"""
            INSERT INTO site_capacity_pools(tenant_id,site_id,vehicle_type,total_capacity,max_reservation_quota)
            VALUES ('{fixture.Owner.TenantId}','{fixture.SiteId}','CAR',1,1)
            """);
    }

    [StructurePostgresFact]
    public async Task ReceiptReplayCannotReleaseANewerHoldAndAbortedTokenCannotBeReused()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await fixture.ReservationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, first, CancellationToken.None);
        await fixture.ReservationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, first, null, CancellationToken.None);
        await fixture.ReservationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, second, CancellationToken.None);

        await fixture.ReservationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, first, null, CancellationToken.None);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation, "SELECT count(*) FROM structure_edit_holds"));
        await Assert.ThrowsAsync<OperationDomain.HoldConflictException>(() =>
            fixture.ReservationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, first, CancellationToken.None));

        // Same token with a different payload is not a valid retry.
        await Assert.ThrowsAsync<OperationDomain.HoldConflictException>(() =>
            fixture.ReservationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, first,
                new(true, [], [], []), CancellationToken.None));
    }

    [StructurePostgresFact]
    public async Task ConcurrentAcquireForTheSameSiteYieldsOneHoldAndACleanConflict()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var firstToken = Guid.NewGuid();
        var secondToken = Guid.NewGuid();

        // Two independent DbContexts, each with its own pre-opened PostgreSQL connection, so the
        // two AcquireAsync calls really run against the same database at the same time.
        await using var firstContext = fixture.CreateReservationContext();
        await using var secondContext = fixture.CreateReservationContext();
        await firstContext.Database.OpenConnectionAsync();
        await secondContext.Database.OpenConnectionAsync();
        var firstStore = new StructureSafetyStore(firstContext);
        var secondStore = new StructureSafetyStore(secondContext);

        using var barrier = new Barrier(2);
        Task<(Guid Token, bool Won, Exception? Error)> Attempt(Guid token, StructureSafetyStore store) =>
            Task.Run(async () =>
            {
                // Release both attempts together so they overlap inside serializable transactions.
                barrier.SignalAndWait();
                try
                {
                    await store.AcquireAsync(fixture.Owner.TenantId, fixture.SiteId, token, CancellationToken.None);
                    return (token, true, (Exception?)null);
                }
                catch (Exception error)
                {
                    return (token, false, error);
                }
            });

        var results = await Task.WhenAll(Attempt(firstToken, firstStore), Attempt(secondToken, secondStore));

        var winner = Assert.Single(results, result => result.Won);
        var loser = Assert.Single(results, result => !result.Won);
        // Exactly one physical hold row, owned by the winning token.
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_edit_holds WHERE site_id='{fixture.SiteId}'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_edit_holds WHERE site_id='{fixture.SiteId}' AND token='{winner.Token}'"));
        // The loser gets the existing clean conflict type, never a raw Npgsql provider exception.
        Assert.IsType<OperationDomain.HoldConflictException>(loser.Error);
        Assert.IsNotType<PostgresException>(loser.Error);
    }

    [StructurePostgresFact]
    public async Task ReleaseRacingAcquireLeavesOneDeterministicHoldState()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var releasing = Guid.NewGuid();
        var claiming = Guid.NewGuid();
        // Seed a committed hold for `releasing` through the normal service path.
        await fixture.ReservationService.PrepareAsync(
            fixture.Owner.TenantId, fixture.SiteId, releasing, CancellationToken.None);

        await using var releaseContext = fixture.CreateReservationContext();
        await using var acquireContext = fixture.CreateReservationContext();
        await releaseContext.Database.OpenConnectionAsync();
        await acquireContext.Database.OpenConnectionAsync();
        var releaseStore = new StructureSafetyStore(releaseContext);
        var acquireStore = new StructureSafetyStore(acquireContext);

        using var barrier = new Barrier(2);
        Task<(bool Won, Exception? Error)> Release() => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            try
            {
                await releaseStore.ReleaseAsync(
                    fixture.Owner.TenantId, fixture.SiteId, releasing, null, CancellationToken.None);
                return (true, (Exception?)null);
            }
            catch (Exception error)
            {
                return (false, error);
            }
        });
        Task<(bool Won, Exception? Error)> Acquire() => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            try
            {
                await acquireStore.AcquireAsync(
                    fixture.Owner.TenantId, fixture.SiteId, claiming, CancellationToken.None);
                return (true, (Exception?)null);
            }
            catch (Exception error)
            {
                return (false, error);
            }
        });

        var results = await Task.WhenAll(Release(), Acquire());
        var released = results[0];
        var acquired = results[1];

        // Releasing a live hold is always valid; the racing acquire either commits after the release
        // or observes the older hold and must surface the clean conflict, never a provider exception.
        Assert.True(released.Won);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_release_receipts WHERE token='{releasing}'"));
        if (acquired.Won)
        {
            Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
                $"SELECT count(*) FROM structure_edit_holds WHERE site_id='{fixture.SiteId}' AND token='{claiming}'"));
        }
        else
        {
            Assert.IsType<OperationDomain.HoldConflictException>(acquired.Error);
            Assert.IsNotType<PostgresException>(acquired.Error);
            Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation,
                $"SELECT count(*) FROM structure_edit_holds WHERE site_id='{fixture.SiteId}'"));
        }
    }

    [StructurePostgresFact]
    public async Task RecoveryFenceWaitsForUnacknowledgedPhysicalCommitAndReturnsReadyPayload()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var pending = new StructureSynchronization(Guid.NewGuid(), fixture.Owner.TenantId, fixture.SiteId, null, "preparing", null);
        await fixture.Store.PrepareAsync(pending, CancellationToken.None);
        var outcome = new StructureOutcome(true, [], [], [new(null, "CAR", 0)]);

        await using var connection = await fixture.Parking.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var update = new NpgsqlCommand(
            "UPDATE structure_synchronizations SET state='ready',outcome=$2::jsonb WHERE token=$1",
            connection, transaction);
        update.Parameters.AddWithValue(pending.Token);
        update.Parameters.AddWithValue(JsonSerializer.Serialize(outcome, StructureSynchronizationJson.Options));
        await update.ExecuteNonQueryAsync();

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var recovery = fixture.Store.FencePreparationAsync(pending.Token, deadline.Token);
        // Prove recovery reached PostgreSQL and is actually waiting for the row.
        while (await fixture.ScalarAsync(fixture.Parking, """
            SELECT count(*) FROM pg_stat_activity
            WHERE datname=current_database() AND wait_event_type='Lock'
                AND query LIKE '%state=''aborting''%'
            """) == 0)
        {
            await Task.Delay(10, deadline.Token);
        }
        // Complete the in-flight physical transaction. PostgreSQL's row lock makes
        // the competing UPDATE observe ready instead of cancelling preparation.
        await transaction.CommitAsync();
        var durable = await recovery;
        Assert.Equal("ready", durable.State);
        Assert.NotNull(durable.Outcome);
        Assert.Equal(0, durable.Outcome.Capacities.Single().Capacity);
    }

    [StructurePostgresFact]
    public async Task RecoveryWinningTheFenceForcesAllLatePhysicalWritesToRollBack()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var pending = new StructureSynchronization(Guid.NewGuid(), fixture.Owner.TenantId, fixture.SiteId, null, "preparing", null);
        await fixture.Store.PrepareAsync(pending, CancellationToken.None);
        var edit = await new PostgresParkingStructureRepository(fixture.Parking).BeginEditAsync(
            fixture.Owner, fixture.SiteId, new([], false), null, CancellationToken.None);

        await using (edit)
        {
            var fenced = await fixture.Store.FencePreparationAsync(pending.Token, CancellationToken.None);
            Assert.Equal("aborting", fenced.State);
            edit.Structure.AddSlot(fixture.UnitId, "LATE", VehicleType.CAR);
            var outcome = StructureOutcomeBuilder.Build(edit.Structure, new HashSet<Guid>(), new HashSet<Guid>());

            await Assert.ThrowsAsync<StructureException>(() =>
                edit.CommitAsync(pending.Token, outcome, null, null, CancellationToken.None));
        }
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Parking, "SELECT count(*) FROM parking_slots"));
    }


    [StructurePostgresFact]
    public async Task GateConnectionsLeaveATwoConnectorQueryPoolAvailable()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var settings = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION"))
        {
            Database = fixture.Parking.ConnectionString is { } connectionString
                ? new NpgsqlConnectionStringBuilder(connectionString).Database : throw new InvalidOperationException(),
            MaxPoolSize = 2
        };
        await using var querySource = NpgsqlDataSource.Create(settings.ConnectionString);
        await using var gates = new ParkingGateConnectionPool(settings.ConnectionString, 2);
        var store = new PostgresStructureSynchronizationStore(querySource, gates);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var first = await store.TryLockSiteAsync(Guid.NewGuid(), deadline.Token);
        await using var second = await store.TryLockSiteAsync(Guid.NewGuid(), deadline.Token);
        Assert.NotNull(first);
        Assert.NotNull(second);
        // Both gates are retained, yet query connectors must still be available.
        await store.PendingAsync(null, deadline.Token);
        await using var rejected = await store.TryLockSiteAsync(Guid.NewGuid(), deadline.Token);
        Assert.Null(rejected);
    }

    [StructurePostgresFact]
    public async Task CompletedOperationCannotBeRevertedByAForegroundErrorUpdate()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var store = new StructureOperationStore(fixture.Parking, fixture.Gates);
        var operation = await store.BeginAsync(
            fixture.Owner, fixture.SiteId, Guid.NewGuid(), new("removeUnit", fixture.UnitId), CancellationToken.None);
        await fixture.ExecuteAsync(fixture.Parking,
            $"UPDATE structure_operations SET status='completed',physical_committed=TRUE WHERE id='{operation.Id}'");

        var result = await store.UpdateAsync(
            fixture.Owner, operation.Id, "pending", "Late transport error", null, CancellationToken.None);
        Assert.Equal("completed", result.Status);
        await Assert.ThrowsAsync<StructureException>(() => store.UpdateAsync(
            new(Guid.NewGuid(), fixture.Owner.TenantId), operation.Id, "pending", "Other actor", null, CancellationToken.None));
    }

    [StructurePostgresFact]
    public async Task ProjectionFailureRollsBackReceiptProjectionAndHoldReleaseTogether()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await fixture.ExecuteAsync(fixture.Reservation, $"""
            INSERT INTO site_capacity_pools(tenant_id,site_id,vehicle_type,total_capacity,max_reservation_quota)
            VALUES ('{fixture.Owner.TenantId}','{fixture.SiteId}','CAR',1,1);
            CREATE FUNCTION reject_test_capacity() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Simulated projection failure' USING ERRCODE='23514'; END $$;
            CREATE TRIGGER reject_test_capacity BEFORE UPDATE ON site_capacity_pools
            FOR EACH ROW EXECUTE FUNCTION reject_test_capacity();
            """);
        var token = Guid.NewGuid();
        var removed = Guid.NewGuid();
        await fixture.ReservationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, token, CancellationToken.None);
        await Assert.ThrowsAsync<PostgresException>(() => fixture.ReservationService.CompleteAsync(
            fixture.Owner.TenantId, fixture.SiteId, token,
            new(false, [removed], [], [new(null, "CAR", 0)]), CancellationToken.None));

        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT count(*) FROM structure_edit_holds"));
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_release_receipts WHERE token='{token}'"));
        // Rollback is proven by the receipt and pool asserts below; the removed-resource
        // tombstone table no longer exists, so guard on the function body instead.
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT count(*) FROM pg_proc WHERE proname='guard_structure_edit' AND pg_get_functiondef(oid) LIKE '%structure_removed_resources%'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_site_state WHERE site_id='{fixture.SiteId}' AND active"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT count(*) FROM site_capacity_pools WHERE total_capacity=1 AND max_reservation_quota=1"));

        await fixture.ExecuteAsync(fixture.Reservation, "DROP TRIGGER reject_test_capacity ON site_capacity_pools");
        await fixture.ReservationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, token,
            new(false, [removed], [], [new(null, "CAR", 0)]), CancellationToken.None);
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation, "SELECT count(*) FROM structure_edit_holds"));
    }

    // ---- SPARK-238 P4 acceptance coverage for the P2 facility-structure behaviour ----

    [StructurePostgresFact]
    public async Task MultiValueCompatibilityRoundTripsThroughTheCompatibilityTable()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var slotId = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01",
            vehicle: null, type: SlotType.STANDARD,
            vehicleTypes: [VehicleType.CAR, VehicleType.MOTORCYCLE],
            primaryVehicleType: VehicleType.CAR, ct: CancellationToken.None);

        var structure = await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);
        var slot = Assert.Single(structure.Slots);
        Assert.Equal(slotId, slot.Id);
        Assert.Equal(VehicleType.CAR, slot.VehicleType); // the explicit primary is the projection column
        Assert.Equal(2, slot.VehicleTypes.Count);
        Assert.Contains(VehicleType.CAR, slot.VehicleTypes);
        Assert.Contains(VehicleType.MOTORCYCLE, slot.VehicleTypes);

        Assert.Equal(2L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM slot_compatibilities WHERE slot_id='{slotId}'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM slot_compatibilities WHERE slot_id='{slotId}' AND vehicle_type='MOTORCYCLE'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM parking_slots WHERE id='{slotId}' AND supported_vehicle_type='CAR' AND deleted_at IS NULL"));
    }

    [StructurePostgresFact]
    public async Task CompatibilityIsAPredicateNotStockForGateClaims()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var slotId = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01",
            vehicle: null, type: SlotType.STANDARD,
            vehicleTypes: [VehicleType.CAR, VehicleType.MOTORCYCLE],
            primaryVehicleType: VehicleType.CAR, ct: CancellationToken.None);
        var claims = new OperatorGateSlotStore(fixture.Parking);

        // The table wins for eligibility: the secondary category can claim the slot.
        var secondary = await claims.ClaimAvailableAsync(fixture.Owner.TenantId, fixture.SiteId, "MOTORCYCLE",
            null, null, false, CancellationToken.None);
        Assert.Equal(slotId, secondary.Id);

        // One physical slot: that claim consumed it, so a CAR claim finds nothing else.
        var exhausted = await Assert.ThrowsAsync<StructureException>(() =>
            claims.ClaimAvailableAsync(fixture.Owner.TenantId, fixture.SiteId, "CAR", null, null, false, CancellationToken.None));
        Assert.Equal("NO_AVAILABLE_SLOT", exhausted.Code);

        await claims.ReleaseAsync(fixture.Owner.TenantId, fixture.SiteId, slotId, CancellationToken.None);
        var primary = await claims.ClaimAvailableAsync(fixture.Owner.TenantId, fixture.SiteId, "CAR",
            null, null, false, CancellationToken.None);
        Assert.Equal(slotId, primary.Id);

        // Pools count each slot once, by primary category, so they still sum to the registered count.
        var structure = await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);
        var sitePools = structure.CapacityViews.Where(view => view.UnitId is null).ToArray();
        Assert.Equal(1, structure.Site.TotalPhysicalCapacity);
        Assert.Equal(1, sitePools.Single(view => view.VehicleType == VehicleType.CAR).Total);
        Assert.Equal(0, sitePools.Single(view => view.VehicleType == VehicleType.MOTORCYCLE).Total);
        Assert.Equal(structure.Site.TotalPhysicalCapacity, sitePools.Sum(view => view.Total));
    }

    [StructurePostgresFact]
    public async Task LegacyProjectionIsTheClaimFallbackAndANullProjectionNeverThrows()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var claims = new OperatorGateSlotStore(fixture.Parking);

        // Legacy row: no compatibility rows, non-null projection column.
        var legacy = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "LEGACY",
            VehicleType.CAR, ct: CancellationToken.None);
        await fixture.ExecuteAsync(fixture.Parking, $"DELETE FROM slot_compatibilities WHERE slot_id='{legacy}'");
        var claimed = await claims.ClaimAvailableAsync(fixture.Owner.TenantId, fixture.SiteId, "CAR",
            null, null, false, CancellationToken.None);
        Assert.Equal(legacy, claimed.Id);
        Assert.Equal("CAR", claimed.VehicleType);

        // Unclassified row: NULL projection and no compatibility rows. Inserted directly; the aggregate
        // read path for NULL columns belongs to SPARK-228, so no service edit is issued afterwards.
        var unclassified = Guid.NewGuid();
        await fixture.ExecuteAsync(fixture.Parking, $"""
            INSERT INTO parking_slots(id,tenant_id,site_id,spatial_unit_id,slot_code,supported_vehicle_type,slot_type)
            VALUES ('{unclassified}','{fixture.Owner.TenantId}','{fixture.SiteId}','{fixture.UnitId}','NULL01',NULL,'STANDARD')
            """);

        // A concrete-type claim is not eligible and must surface NO_AVAILABLE_SLOT, never a DB error.
        var missing = await Assert.ThrowsAsync<StructureException>(() =>
            claims.ClaimAvailableAsync(fixture.Owner.TenantId, fixture.SiteId, "CAR", null, null, false, CancellationToken.None));
        Assert.Equal("NO_AVAILABLE_SLOT", missing.Code);

        // The ANY path still reads the NULL projection safely.
        var any = await claims.ClaimAvailableAsync(fixture.Owner.TenantId, fixture.SiteId, "ANY",
            null, null, false, CancellationToken.None);
        Assert.Equal(unclassified, any.Id);
        Assert.Equal(string.Empty, any.VehicleType);
    }

    [StructurePostgresFact]
    public async Task OwnerGeometryAndFeaturesWritesAreRejectedAndStoredValuesSurvive()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var slotId = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01",
            VehicleType.CAR, ct: CancellationToken.None);
        await fixture.ExecuteAsync(fixture.Parking, $$"""
            UPDATE parking_slots SET coordinates_3d='{"x":9}'::jsonb, features='{"ev":true}'::jsonb WHERE id='{{slotId}}'
            """);

        var geometry = await Assert.ThrowsAsync<StructureException>(() => fixture.Service.ApplyAsync(
            fixture.Owner, fixture.SiteId,
            new StructureEdit("configureSlot", ResourceId: slotId, VehicleType: VehicleType.MOTORCYCLE,
                SlotType: SlotType.STANDARD, Coordinates3D: "{\"x\":1}")));
        Assert.Equal("INVALID_STRUCTURE", geometry.Code);
        Assert.Equal("Slot geometry comes from the lot's fixed 3D template.", geometry.Message);

        var features = await Assert.ThrowsAsync<StructureException>(() => fixture.Service.ApplyAsync(
            fixture.Owner, fixture.SiteId,
            new StructureEdit("configureSlot", ResourceId: slotId, VehicleType: VehicleType.MOTORCYCLE,
                SlotType: SlotType.STANDARD, Features: "{\"ev\":false}")));
        Assert.Equal("INVALID_STRUCTURE", features.Code);
        Assert.Equal("Slot geometry comes from the lot's fixed 3D template.", features.Message);

        // A valid configure still passes the server-owned values through untouched.
        await fixture.Service.ApplyAsync(fixture.Owner, fixture.SiteId,
            new StructureEdit("configureSlot", ResourceId: slotId, VehicleType: VehicleType.CAR, SlotType: SlotType.VIP));

        var stored = await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);
        Assert.Equal(SlotType.VIP, Assert.Single(stored.Slots).Type);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking, $"""
            SELECT count(*) FROM parking_slots
            WHERE id='{slotId}' AND coordinates_3d->>'x'='9' AND features->>'ev'='true'
            """));
    }

    [StructurePostgresFact]
    public async Task ReleasedSlotCodesRequireAnExplicitRestore()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var slotId = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A-01",
            vehicle: null, type: SlotType.STANDARD,
            vehicleTypes: [VehicleType.CAR, VehicleType.MOTORCYCLE],
            primaryVehicleType: VehicleType.CAR, ct: CancellationToken.None);
        await fixture.Service.ApplyAsync(fixture.Owner, fixture.SiteId,
            new StructureEdit("removeSlot", ResourceId: slotId));

        // Create never resurrects: the released natural key blocks it and nothing is written.
        var released = await Assert.ThrowsAsync<ReleasedIdentifierException>(() =>
            fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "a-01", VehicleType.CAR));
        Assert.Equal("IDENTIFIER_RELEASED", released.Code);
        Assert.Equal(slotId, released.ResourceId);
        Assert.Equal("A-01", released.ReleasedCode);
        Assert.NotEqual(default, released.DeletedAt);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            "SELECT count(*) FROM parking_slots WHERE upper(slot_code)='A-01'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            "SELECT count(*) FROM parking_slots WHERE upper(slot_code)='A-01' AND deleted_at IS NOT NULL"));

        // Explicit restore keeps the row id and counts the slot once.
        await fixture.Service.ApplyAsync(fixture.Owner, fixture.SiteId,
            new StructureEdit("restoreSlot", ResourceId: slotId));
        var restored = await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);
        Assert.Equal(slotId, Assert.Single(restored.Slots).Id);
        Assert.Empty(restored.ReleasedSlots);
        Assert.Equal(1, restored.Site.TotalPhysicalCapacity);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM parking_slots WHERE id='{slotId}' AND deleted_at IS NULL"));

        // Afterwards the code is a live duplicate, not a released one.
        var duplicate = await Assert.ThrowsAsync<StructureException>(() =>
            fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A-01", VehicleType.CAR));
        Assert.Equal("DUPLICATE_IDENTIFIER", duplicate.Code);

        // The compatibility set survived the soft delete: the restored slot is claimable for its secondary type.
        var claims = new OperatorGateSlotStore(fixture.Parking);
        var claimed = await claims.ClaimAvailableAsync(fixture.Owner.TenantId, fixture.SiteId, "MOTORCYCLE",
            null, null, false, CancellationToken.None);
        Assert.Equal(slotId, claimed.Id);
    }

    [StructurePostgresFact]
    public async Task StaleDeclaredCapacityIsReconciledInsteadOfBlockingTheEdit()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01", VehicleType.CAR);
        await fixture.ExecuteAsync(fixture.Parking,
            $"UPDATE parking_sites SET total_physical_capacity=99 WHERE id='{fixture.SiteId}'");

        // The stale declared value no longer blocks the edit: the derived count is corrected on write.
        await fixture.Service.ApplyAsync(fixture.Owner, fixture.SiteId,
            new StructureEdit("profile", Code: "TEST", Name: "Test", Address: "Address"));

        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT total_physical_capacity FROM parking_sites WHERE id='{fixture.SiteId}'"));
        var structure = await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);
        Assert.Equal(1, structure.Site.TotalPhysicalCapacity);
        Assert.Single(structure.Slots);
        var sitePools = structure.CapacityViews.Where(view => view.UnitId is null).ToArray();
        Assert.Equal(structure.Slots.Count, sitePools.Sum(view => view.Total));
    }

    [StructurePostgresFact]
    public async Task FutureMaintenanceWindowIsScheduledAndHalfOpenOverlapIsEnforced()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var windows = MaintenanceWindows(fixture);
        var now = DateTime.UtcNow;
        var start = now.AddHours(1);
        var end = now.AddHours(2);

        var created = await windows.CreateAsync(fixture.Owner, fixture.SiteId, start, end, "Repaving", null, null, CancellationToken.None);
        Assert.Equal("pending", created.EnforcementState);
        Assert.Null(created.CancelledAt);

        // A future closure is scheduled, never MAINTENANCE, and the lot stays lifecycle-ACTIVE.
        var state = await windows.StateAsync(fixture.Owner, fixture.SiteId, "ACTIVE", CancellationToken.None);
        Assert.Equal(MaintenanceWindowState.Scheduled, state.State);
        Assert.Null(state.ClosedUntil);
        Assert.Single(state.Windows);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM parking_sites WHERE id='{fixture.SiteId}' AND status='ACTIVE'"));

        // Overlap goes through the DB exclusion constraint (23P01 -> STRUCTURE_IN_USE).
        var overlap = await Assert.ThrowsAsync<StructureException>(() => windows.CreateAsync(
            fixture.Owner, fixture.SiteId, end.AddMinutes(-30), end.AddHours(1), "Overlap", null, null, CancellationToken.None));
        Assert.Equal("STRUCTURE_IN_USE", overlap.Code);
        Assert.Equal("Another maintenance window already covers this period.", overlap.Message);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking, "SELECT count(*) FROM structure_maintenance_windows"));

        // Half-open [start, end): starting exactly at the first window's end is adjacent, not overlapping.
        var adjacent = await windows.CreateAsync(fixture.Owner, fixture.SiteId, end, end.AddHours(1), "Adjacent", null, null, CancellationToken.None);
        Assert.True((adjacent.Start - end).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.Equal(2L, await fixture.ScalarAsync(fixture.Parking, "SELECT count(*) FROM structure_maintenance_windows"));
    }

    [StructurePostgresFact]
    public async Task MaintenanceWindowIsGrowOnlyAndCancellationCannotBypassAnotherClosure()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var windows = MaintenanceWindows(fixture);
        var now = DateTime.UtcNow;
        var start = now.AddHours(1);
        var end = now.AddHours(2);
        var window = await windows.CreateAsync(fixture.Owner, fixture.SiteId, start, end, "Repaving", null, null, CancellationToken.None);

        var shortened = await Assert.ThrowsAsync<StructureException>(() => windows.ExtendAsync(
            fixture.Owner, fixture.SiteId, window.Id, null, now.AddMinutes(90), CancellationToken.None));
        Assert.Equal("STRUCTURE_IN_USE", shortened.Code);

        var moved = await Assert.ThrowsAsync<StructureException>(() => windows.ExtendAsync(
            fixture.Owner, fixture.SiteId, window.Id, start.AddHours(-1), end.AddHours(1), CancellationToken.None));
        Assert.Equal("STRUCTURE_IN_USE", moved.Code);

        // Growing is allowed and re-arms the pending acknowledgement state.
        var grown = await windows.ExtendAsync(fixture.Owner, fixture.SiteId, window.Id, null, end.AddHours(1), CancellationToken.None);
        Assert.Equal("pending", grown.EnforcementState);
        Assert.True((grown.End - end.AddHours(1)).Duration() < TimeSpan.FromMilliseconds(1));

        // A window that already ended cannot be extended (seeded to the past: create rejects past starts).
        var ended = await windows.CreateAsync(fixture.Owner, fixture.SiteId, now.AddHours(4), now.AddHours(5), "Past", null, null, CancellationToken.None);
        var endedStart = start.AddHours(-4);
        var endedEnd = start.AddHours(-3);
        await fixture.ExecuteAsync(fixture.Parking,
            $"UPDATE structure_maintenance_windows SET service_window=tstzrange('{endedStart:O}','{endedEnd:O}','[)') WHERE id='{ended.Id}'");
        var endedError = await Assert.ThrowsAsync<StructureException>(() => windows.ExtendAsync(
            fixture.Owner, fixture.SiteId, ended.Id, null, now.AddHours(6), CancellationToken.None));
        Assert.Equal("STRUCTURE_IN_USE", endedError.Code);

        // A live closure covering `now` blocks cancelling a future window (reopening cannot bypass it).
        // Kept clearly inside the gap before the grown window so boundary rounding cannot interfere.
        var current = Guid.NewGuid();
        var currentStart = now.AddMinutes(-30);
        var currentEnd = now.AddMinutes(30);
        await fixture.ExecuteAsync(fixture.Parking, $"""
            INSERT INTO structure_maintenance_windows(id,tenant_id,site_id,service_window,reason,created_by,enforcement_state)
            VALUES ('{current}','{fixture.Owner.TenantId}','{fixture.SiteId}',
                tstzrange('{currentStart:O}','{currentEnd:O}','[)'),'Current closure','{fixture.Owner.UserId}','pending')
            """);
        var blocked = await Assert.ThrowsAsync<StructureException>(() => windows.CancelAsync(
            fixture.Owner, fixture.SiteId, grown.Id, CancellationToken.None));
        Assert.Equal("STRUCTURE_IN_USE", blocked.Code);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM structure_maintenance_windows WHERE id='{grown.Id}' AND cancelled_at IS NULL"));

        // Cancelling the closure that covers now is allowed, then the future window can be cancelled too.
        await windows.CancelAsync(fixture.Owner, fixture.SiteId, current, CancellationToken.None);
        await windows.CancelAsync(fixture.Owner, fixture.SiteId, grown.Id, CancellationToken.None);
        await windows.CancelAsync(fixture.Owner, fixture.SiteId, ended.Id, CancellationToken.None);
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Parking,
            "SELECT count(*) FROM structure_maintenance_windows WHERE cancelled_at IS NULL"));
    }

    [StructurePostgresFact]
    public async Task BatchWithOneFailedItemAppliesNothingAndRecordsTheFailure()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01", VehicleType.CAR);
        await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A02", VehicleType.CAR);
        var audits = await fixture.ScalarAsync(fixture.Parking, "SELECT count(*) FROM structure_change_audit");
        var operations = Operations(fixture);
        var key = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<StructureBatchException>(() => operations.ExecuteAsync(
            fixture.Owner, fixture.SiteId, key,
            new StructureEdit("batch", BatchItems:
            [
                new StructureBatchItem(first, "removeSlot"),
                new StructureBatchItem(fixture.UnitId, "removeUnit") // still holds A02
            ]), CancellationToken.None));

        var failure = Assert.Single(error.Failures);
        Assert.Equal(fixture.UnitId, failure.ResourceId);
        Assert.Equal("UNIT_NOT_EMPTY", failure.Code);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM structure_operations WHERE idempotency_key='{key}' AND status='failed'"));

        // All-or-nothing: nothing was committed and the aggregate is unchanged.
        var structure = await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);
        Assert.Equal(2, structure.Slots.Count);
        Assert.Single(structure.Units);
        Assert.Equal(2, structure.Site.TotalPhysicalCapacity);
        Assert.Equal(audits, await fixture.ScalarAsync(fixture.Parking, "SELECT count(*) FROM structure_change_audit"));
    }

    [StructurePostgresFact]
    public async Task FullyValidBatchAppliesEveryItemOnce()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01", VehicleType.CAR);
        var second = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A02", VehicleType.CAR);
        var operations = Operations(fixture);
        var key = Guid.NewGuid();

        var result = await operations.ExecuteAsync(fixture.Owner, fixture.SiteId, key,
            new StructureEdit("batch", BatchItems:
            [
                new StructureBatchItem(first, "markBackup"),
                new StructureBatchItem(second, "removeSlot")
            ]), CancellationToken.None);

        Assert.Equal("completed", result.Status);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM parking_slots WHERE id='{first}' AND reservation_state='BACKUP' AND deleted_at IS NULL"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM parking_slots WHERE id='{second}' AND deleted_at IS NOT NULL"));
        var structure = await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);
        Assert.Equal(first, Assert.Single(structure.Slots).Id);
        Assert.Equal("BACKUP", structure.Slots.Single().ReservationState);
        Assert.Equal(1, structure.Site.TotalPhysicalCapacity);
    }

    [StructurePostgresFact]
    public async Task StaleExpectedUpdatedAtRejectsTheWholeBatch()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var slot = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01", VehicleType.CAR);
        var before = await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);
        var version = before.UpdatedAt(slot);
        Assert.NotNull(version);
        var operations = Operations(fixture);

        var error = await Assert.ThrowsAsync<StructureBatchException>(() => operations.ExecuteAsync(
            fixture.Owner, fixture.SiteId, Guid.NewGuid(),
            new StructureEdit("batch", BatchItems:
            [
                new StructureBatchItem(slot, "removeSlot", version!.Value.AddSeconds(-1))
            ]), CancellationToken.None));

        Assert.Equal("STRUCTURE_CONFLICT", Assert.Single(error.Failures).Code);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM parking_slots WHERE id='{slot}' AND deleted_at IS NULL"));
    }

    [StructurePostgresFact]
    public async Task BatchRejectsUnsupportedActionsAndItemCountsOutsideOneToFifty()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var operations = Operations(fixture);

        var unsupported = await operations.ExecuteAsync(fixture.Owner, fixture.SiteId, Guid.NewGuid(),
            new StructureEdit("batch", BatchItems: [new StructureBatchItem(fixture.SiteId, "profile")]),
            CancellationToken.None);
        Assert.Equal("failed", unsupported.Status);
        Assert.Contains("Unsupported batch action", unsupported.Reason);

        var empty = await operations.ExecuteAsync(fixture.Owner, fixture.SiteId, Guid.NewGuid(),
            new StructureEdit("batch", BatchItems: []), CancellationToken.None);
        Assert.Equal("failed", empty.Status);
        Assert.Contains("1â€“50 items", empty.Reason);
    }

    [StructurePostgresFact]
    public async Task OperationIdempotencyReplaysOneRecordAndRejectsADifferentPayload()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var slot = await fixture.Service.AddSlotAsync(fixture.Owner, fixture.SiteId, fixture.UnitId, "A01", VehicleType.CAR);
        var operations = Operations(fixture);
        var key = Guid.NewGuid();
        var edit = new StructureEdit("removeSlot", ResourceId: slot);

        var first = await operations.ExecuteAsync(fixture.Owner, fixture.SiteId, key, edit, CancellationToken.None);
        var replay = await operations.ExecuteAsync(fixture.Owner, fixture.SiteId, key, edit, CancellationToken.None);

        Assert.Equal("completed", first.Status);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal("completed", replay.Status);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM structure_operations WHERE idempotency_key='{key}'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking,
            $"SELECT count(*) FROM parking_slots WHERE id='{slot}' AND deleted_at IS NOT NULL"));

        // Same key, different request is a conflict, not a second operation.
        var different = await Assert.ThrowsAsync<StructureException>(() => operations.ExecuteAsync(
            fixture.Owner, fixture.SiteId, key, new StructureEdit("removeUnit", ResourceId: fixture.UnitId),
            CancellationToken.None));
        Assert.Equal("DUPLICATE_IDENTIFIER", different.Code);
    }

    private static MaintenanceWindowService MaintenanceWindows(DatabaseFixture fixture)
        => new(new PostgresMaintenanceWindowStore(fixture.Parking), fixture);

    private static StructureOperationService Operations(DatabaseFixture fixture)
        => new(new StructureOperationStore(fixture.Parking, fixture.Gates), new ResolvedPlanner(), fixture.Service, fixture);

    private sealed class ResolvedPlanner : IStructureImpactPlanner
    {
        public Task<ReallocationPlan> PlanAsync(
            OwnerScope owner, Guid siteId, Guid operationId, StructureEdit edit, CancellationToken cancellationToken)
            => Task.FromResult(new ReallocationPlan("resolved", null, "{}"));
    }

    private sealed class DatabaseFixture : IAsyncDisposable, IOwnerAuthorizer, ISlotOperatorAuthorizer
    {
        private readonly string adminConnection;
        private readonly string parkingDatabase;
        private readonly string reservationDatabase;
        public NpgsqlDataSource Parking { get; }
        public NpgsqlDataSource Reservation { get; }
        private ReservationDbContext ReservationContext { get; }
        // A fresh context on the fixture's reservation database, used for cross-connection tests.
        public ReservationDbContext CreateReservationContext() =>
            new(new DbContextOptionsBuilder<ReservationDbContext>().UseNpgsql(Reservation).Options);
        public OwnerScope Owner { get; } = new(Guid.NewGuid(), Guid.NewGuid());
        public Guid SiteId { get; private set; }
        public Guid UnitId { get; private set; }
        public PostgresStructureSynchronizationStore Store { get; }
        public ParkingGateConnectionPool Gates { get; }
        public OperationApplication.StructureSafetyService ReservationService { get; }
        public ReservationBridge Transport { get; }
        public ParkingStructureService Service { get; }

        private DatabaseFixture(string admin, string parking, string reservation)
        {
            adminConnection = admin;
            parkingDatabase = parking;
            reservationDatabase = reservation;
            var settings = new NpgsqlConnectionStringBuilder(admin) { Database = parking };
            var parkingConnection = settings.ConnectionString;
            Parking = NpgsqlDataSource.Create(parkingConnection);
            settings.Database = reservation;
            Reservation = NpgsqlDataSource.Create(settings.ConnectionString);
            ReservationContext = new ReservationDbContext(new DbContextOptionsBuilder<ReservationDbContext>()
                .UseNpgsql(Reservation).Options);
            Gates = new(parkingConnection);
            Store = new(Parking, Gates);
            ReservationService = new(new StructureSafetyStore(ReservationContext));
            Transport = new(ReservationService);
            var synchronization = new StructureSynchronizationService(Store, Transport);
            Service = new(new PostgresParkingStructureRepository(Parking), this, this, Transport, Store, synchronization);
        }

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var admin = Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION")!;
            // Names contain only a fixed prefix and our own random GUID, never user input.
            var suffix = Guid.NewGuid().ToString("N");
            var parking = "structure_parking_test_" + suffix;
            var reservation = "structure_reservation_test_" + suffix;
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {parking}", connection).ExecuteNonQueryAsync();
            await new NpgsqlCommand($"CREATE DATABASE {reservation}", connection).ExecuteNonQueryAsync();
            var fixture = new DatabaseFixture(admin, parking, reservation);
            try
            {
                var root = FindRepositoryRoot();
                // Post-split facility schema (smartpark_parking). Overlay order follows each file's header.
                await fixture.ApplyAsync(fixture.Parking, root,
                    "scripts/database/microservices/02-facility-service-db.sql",
                    "scripts/database/microservices/03a-parking-structure-ops-service-db.sql",
                    "scripts/database/overlay/parking-tenants.sql",
                    "scripts/database/overlay/parking-structure-synchronizations.sql",
                    "scripts/database/overlay/parking-slot-constraints.sql",
                    "scripts/database/overlay/parking-operator-tables.sql",
                    "scripts/database/overlay/parking-structure-maintenance.sql");
                // Post-split reservation schema (smartpark_reservation). Pools first, then the safety
                // gates that trigger on site_capacity_pools, then receipts, then confirmation time.
                await fixture.ApplyAsync(fixture.Reservation, root,
                    "scripts/database/microservices/03b-reservation-operations-service-db.sql",
                    "scripts/database/overlay/reservation-site-capacity-pools.sql",
                    "scripts/database/overlay/reservation-structure-safety.sql",
                    "scripts/database/overlay/reservation-structure-receipts.sql",
                    "scripts/database/overlay/reservation-confirmation-time.sql");
                await fixture.ExecuteAsync(fixture.Parking, $"""
                    INSERT INTO tenants(id,code,name,contact_email,contact_phone)
                    VALUES ('{fixture.Owner.TenantId}','TEST','Test','test@example.com','0000000000')
                    """);
                var site = await fixture.Service.CreateAsync(fixture.Owner, "TEST", "Test", "Address");
                fixture.SiteId = site.Id;
                fixture.UnitId = await fixture.Service.AddUnitAsync(fixture.Owner, site.Id, UnitType.ZONE, "A");
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SmartParking.slnx"))) return directory.FullName;
                directory = directory.Parent;
            }
            throw new InvalidOperationException("Repository root is not available.");
        }

        private async Task ApplyAsync(NpgsqlDataSource source, string root, params string[] files)
        {
            foreach (var file in files)
                await ExecuteAsync(source, await File.ReadAllTextAsync(Path.Combine(root, file)));
        }

        public async Task ExecuteAsync(NpgsqlDataSource source, string sql)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<long> ScalarAsync(NpgsqlDataSource source, string sql)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            var value = await command.ExecuteScalarAsync();
            // Some fixture queries return integer literals (e.g. CASE ... THEN 1 ELSE 0), not bigint.
            return value is null or DBNull ? 0L : Convert.ToInt64(value);
        }

        public Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken ct) => Task.FromResult(owner == Owner);
        public Task<OwnerScope> ScopeAsync(Guid site, CancellationToken ct) => Task.FromResult(Owner);
        public Task<bool> IsAuthorizedAsync(OwnerScope owner, Guid site, CancellationToken ct)
            => Task.FromResult(owner == Owner && site == SiteId);

        public async ValueTask DisposeAsync()
        {
            await Gates.DisposeAsync();
            await ReservationContext.DisposeAsync();
            await Parking.DisposeAsync();
            await Reservation.DisposeAsync();
            await using var connection = new NpgsqlConnection(adminConnection);
            await connection.OpenAsync();
            // Only the two databases with generated names belonging to this fixture.
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {parkingDatabase} WITH (FORCE)", connection).ExecuteNonQueryAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {reservationDatabase} WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }

    private sealed class ReservationBridge(OperationApplication.StructureSafetyService service) : IStructureCommitments
    {
        public bool FailAfterRelease { get; set; }
        public async Task<CommitmentSnapshot> ReadSnapshotAsync(Guid tenant, Guid site, CancellationToken ct)
        {
            var snapshot = await service.ReadSnapshotAsync(tenant, site, ct);
            return new(snapshot.ProtectedSlots, snapshot.HasLiveCommitments,
                snapshot.Capacities?.Select(claim => new CapacityClaim(
                    claim.UnitId, claim.VehicleType, claim.PendingPayment, claim.Protected)).ToArray());
        }

        public async Task<CommitmentSnapshot> AcquireAsync(Guid tenant, Guid site, Guid token, CancellationToken ct)
        {
            var snapshot = await service.PrepareAsync(tenant, site, token, ct);
            return new(snapshot.ProtectedSlots, snapshot.HasLiveCommitments,
                snapshot.Capacities?.Select(claim => new CapacityClaim(
                    claim.UnitId, claim.VehicleType, claim.PendingPayment, claim.Protected)).ToArray());
        }
        public async Task ReleaseAsync(Guid tenant, Guid site, Guid token, StructureOutcome? outcome, CancellationToken ct)
        {
            var reservationOutcome = outcome is null ? null : new OperationDomain.EditOutcome(
                outcome.SiteActive, outcome.RemovedSlots, outcome.RemovedUnits,
                outcome.Capacities.Select(pool => new OperationDomain.PoolCapacity(
                    pool.UnitId, pool.VehicleType, pool.Capacity, pool.Backup)).ToArray());
            await service.CompleteAsync(tenant, site, token, reservationOutcome, ct);
            if (FailAfterRelease)
            {
                FailAfterRelease = false;
                throw new StructureException("COMMITMENTS_UNAVAILABLE", "Simulated lost HTTP acknowledgement after commit.");
            }
        }

        public Task ForceReleaseAsync(Guid tenant, Guid site, CancellationToken ct)
            => service.ForceReleaseAsync(tenant, site, ct);
    }
}
