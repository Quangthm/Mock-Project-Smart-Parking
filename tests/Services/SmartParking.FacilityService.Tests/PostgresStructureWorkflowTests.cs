using Npgsql;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Persistence;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Domain.Entities;
using ReservationApplication = SmartParking.OperationService.Application;
using ReservationDomain = SmartParking.OperationService.Domain;

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
    public async Task OwnerOnboardingIsAtomicRetryableAndPreservesSubsequentEdits()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var id = Guid.NewGuid();
        var body = new OwnerOnboardingBody(id, "Business", "owner@example.com", "0911111111",
            JsonSerializer.SerializeToElement(new { name = "Submitted lot", address = "Address", totalSlots = 20, floors = 2 }));
        var store = new OwnerOnboardingStore(fixture.Parking);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => store.ProvisionAsync(id, body, default)));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking, $"SELECT count(*) FROM parking_sites WHERE id='{id}' AND total_physical_capacity=0 AND status='ACTIVE'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking, $"SELECT count(*) FROM owner_onboarding_provisions WHERE application_id='{id}' AND payload->'Lot'->>'totalSlots'='20'"));
        await Assert.ThrowsAsync<StructureException>(() => store.ProvisionAsync(id, body with { Name = "Changed" }, default));
        await fixture.ExecuteAsync(fixture.Parking, $"UPDATE parking_sites SET name='Owner edited',status='INACTIVE' WHERE id='{id}'");
        await new OwnerOnboardingStore(fixture.Parking).ProvisionAsync(id, body, default);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Parking, $"SELECT count(*) FROM parking_sites WHERE id='{id}' AND name='Owner edited' AND status='INACTIVE'"));
        var conflict = Guid.NewGuid();
        await fixture.ExecuteAsync(fixture.Parking, $"INSERT INTO parking_sites(id,tenant_id,site_code,name,address) VALUES ('{conflict}','{fixture.Owner.TenantId}','conflict','existing','address')");
        await Assert.ThrowsAsync<PostgresException>(() => store.ProvisionAsync(conflict, body with { TenantId = conflict }, default));
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Parking, $"SELECT count(*) FROM tenants WHERE id='{conflict}'"));
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
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_removed_resources WHERE id='{slotId}'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_release_receipts WHERE token='{pending.Token}'"));

    }

    [StructurePostgresFact]
    public async Task ReservationTriggerBlocksCapacityWritesUntilTheHoldCompletes()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var token = Guid.NewGuid();
        await fixture.OperationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, token, CancellationToken.None);

        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.ExecuteAsync(fixture.Reservation, $"""
            INSERT INTO site_capacity_pools(tenant_id,site_id,vehicle_type,total_capacity,max_reservation_quota)
            VALUES ('{fixture.Owner.TenantId}','{fixture.SiteId}','CAR',1,1)
            """));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);

        await fixture.OperationService.CompleteAsync(
            fixture.Owner.TenantId, fixture.SiteId, token, null, CancellationToken.None);
        await fixture.ExecuteAsync(fixture.Reservation, $"""
            INSERT INTO site_capacity_pools(tenant_id,site_id,vehicle_type,total_capacity,max_reservation_quota)
            VALUES ('{fixture.Owner.TenantId}','{fixture.SiteId}','CAR',1,1)
            """);
    }

    [StructurePostgresFact]
    public async Task ReactivatingSitePublishesStateBeforeCapacityWithBackup()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await fixture.ExecuteAsync(fixture.Reservation, $"""
            INSERT INTO site_capacity_pools(tenant_id,site_id,vehicle_type,total_capacity,max_reservation_quota)
            VALUES ('{fixture.Owner.TenantId}','{fixture.SiteId}','CAR',2,1);
            UPDATE structure_site_state SET active=false WHERE site_id='{fixture.SiteId}';
            """);
        var token = Guid.NewGuid();
        await fixture.OperationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, token, CancellationToken.None);

        await fixture.OperationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, token,
            new(true, [], [], [new(null, "CAR", 2, 1)]), CancellationToken.None);

        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_site_state WHERE site_id='{fixture.SiteId}' AND active"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT count(*) FROM site_capacity_pools WHERE total_capacity=2 AND backup_count=1"));
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation, "SELECT count(*) FROM structure_edit_holds"));
    }
    [StructurePostgresFact]
    public async Task ReceiptReplayCannotReleaseANewerHoldAndAbortedTokenCannotBeReused()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await fixture.OperationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, first, CancellationToken.None);
        await fixture.OperationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, first, null, CancellationToken.None);
        await fixture.OperationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, second, CancellationToken.None);

        await fixture.OperationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, first, null, CancellationToken.None);
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation, "SELECT count(*) FROM structure_edit_holds"));
        await Assert.ThrowsAsync<ReservationDomain.HoldConflictException>(() =>
            fixture.OperationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, first, CancellationToken.None));

        // Same token with a different payload is not a valid retry.
        await Assert.ThrowsAsync<ReservationDomain.HoldConflictException>(() =>
            fixture.OperationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, first,
                new(true, [], [], []), CancellationToken.None));
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
        await fixture.OperationService.PrepareAsync(fixture.Owner.TenantId, fixture.SiteId, token, CancellationToken.None);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.OperationService.CompleteAsync(
            fixture.Owner.TenantId, fixture.SiteId, token,
            new(false, [removed], [], [new(null, "CAR", 0)]), CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
        // A retry uses a fresh request scope after the failed EF transaction.
        fixture.ReservationContext.ChangeTracker.Clear();

        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT count(*) FROM structure_edit_holds"));
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_release_receipts WHERE token='{token}'"));
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_removed_resources WHERE id='{removed}'"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            $"SELECT count(*) FROM structure_site_state WHERE site_id='{fixture.SiteId}' AND active"));
        Assert.Equal(1L, await fixture.ScalarAsync(fixture.Reservation,
            "SELECT count(*) FROM site_capacity_pools WHERE total_capacity=1 AND max_reservation_quota=1"));

        await fixture.ExecuteAsync(fixture.Reservation, "DROP TRIGGER reject_test_capacity ON site_capacity_pools");
        await fixture.OperationService.CompleteAsync(fixture.Owner.TenantId, fixture.SiteId, token,
            new(false, [removed], [], [new(null, "CAR", 0)]), CancellationToken.None);
        Assert.Equal(0L, await fixture.ScalarAsync(fixture.Reservation, "SELECT count(*) FROM structure_edit_holds"));
    }

    private sealed class DatabaseFixture : IAsyncDisposable, IOwnerAuthorizer, ISlotOperatorAuthorizer
    {
        private readonly string adminConnection;
        private readonly string parkingDatabase;
        private readonly string reservationDatabase;
        public NpgsqlDataSource Parking { get; }
        public NpgsqlDataSource Reservation { get; }
        public ReservationDbContext ReservationContext { get; }
        public OwnerScope Owner { get; } = new(Guid.NewGuid(), Guid.NewGuid());
        public Guid SiteId { get; private set; }
        public Guid UnitId { get; private set; }
        public PostgresStructureSynchronizationStore Store { get; }
        public ParkingGateConnectionPool Gates { get; }
        public ReservationApplication.StructureSafetyService OperationService { get; }
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
            OperationService = new(new StructureSafetyStore(ReservationContext));
            Transport = new(OperationService);
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
                await fixture.ApplyAsync(fixture.Parking, root,
                    "scripts/database/microservices/02-facility-service-db.sql",
                    "scripts/database/microservices/03a-parking-structure-ops-service-db.sql",
                    "scripts/database/overlay/parking-tenants.sql",
                    "scripts/database/overlay/parking-structure-synchronizations.sql",
                    "scripts/database/overlay/parking-operator-tables.sql",
                    "scripts/database/overlay/parking-slot-constraints.sql",
                    "scripts/database/05.16-Parking-Owner-Onboarding.sql",
                    "scripts/database/05.16-Parking-Owner-Onboarding.sql");
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
            return (long)(await command.ExecuteScalarAsync())!;
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

    private sealed class ReservationBridge(ReservationApplication.StructureSafetyService service) : IStructureCommitments
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
            var reservationOutcome = outcome is null ? null : new ReservationDomain.EditOutcome(
                outcome.SiteActive, outcome.RemovedSlots, outcome.RemovedUnits,
                outcome.Capacities.Select(pool => new ReservationDomain.PoolCapacity(
                    pool.UnitId, pool.VehicleType, pool.Capacity, pool.Backup)).ToArray());
            await service.CompleteAsync(tenant, site, token, reservationOutcome, ct);
            if (FailAfterRelease)
            {
                FailAfterRelease = false;
                throw new StructureException("COMMITMENTS_UNAVAILABLE", "Simulated lost HTTP acknowledgement after commit.");
            }
        }
    }
}
