using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using Xunit;

namespace SmartParking.OperationService.Tests;

/// <summary>
/// Runs only when an isolated PostgreSQL server is available. The fixture creates a brand new
/// database, applies the reservation/operations schema, and drops only that generated database;
/// the running application databases are never touched.
/// </summary>
public sealed class OperationPolicyPostgresFactAttribute : FactAttribute
{
    public OperationPolicyPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_OPERATION_POLICY_TEST_CONNECTION")))
        {
            Skip = "Set SMARTPARK_OPERATION_POLICY_TEST_CONNECTION to an isolated PostgreSQL server " +
                   "(with CREATE DATABASE permission) to run the two-connection concurrency proof.";
        }
    }
}

/// <summary>
/// SPARK-370 P1: the platform/site compatibility decision must be serialised across two real
/// PostgreSQL transactions. Owner publish and Admin activation acquire the same
/// <see cref="PolicyCompatibility"/> advisory lock, so exactly one ordering can commit and the
/// database can never contain incompatible platform and site policies.
/// </summary>
public class PlatformPolicyPostgresConcurrencyTests
{
    private const string WideBounds = "{\"RESERVATION_MIN_DURATION\":{\"Minimum\":15,\"Maximum\":120}}";
    private const string TightBounds = "{\"RESERVATION_MIN_DURATION\":{\"Minimum\":15,\"Maximum\":60}}";

    [OperationPolicyPostgresFact, Trait("Category", "Postgres")]
    public async Task ConcurrentOwnerPublishAndAdminActivation_ExactlyOneWinsAndStateStaysCompatible()
    {
        await using var fixture = await TempDatabase.CreateAsync();

        for (var iteration = 0; iteration < 4; iteration++)
        {
            var scenario = await SeedAsync(fixture);

            // A third connection holds the shared gate so both operations genuinely queue on it
            // and then run against the same database at the same time.
            await using var holder = new NpgsqlConnection(fixture.ConnectionString);
            await holder.OpenAsync();
            await using var holdTransaction = await holder.BeginTransactionAsync();
            await PolicyCompatibility.AcquireOnConnectionAsync(holder, CancellationToken.None);

            var ownerTask = Task.Run(() => TryPublishAsync(fixture.ConnectionString, scenario));
            var adminTask = Task.Run(() => TryActivateAsync(fixture.ConnectionString, scenario));

            await WaitForAdvisoryWaitersAsync(fixture.ConnectionString, expected: 2, TimeSpan.FromSeconds(10));
            await holdTransaction.RollbackAsync();

            var results = await Task.WhenAll(ownerTask, adminTask);
            var owner = results[0];
            var admin = results[1];

            // The shared gate serialises the two decisions, so exactly one must commit.
            Assert.NotEqual(owner.Won, admin.Won);
            var loser = owner.Won ? admin : owner;
            Assert.True(
                loser.Error is PolicyOutOfBoundsException or PolicyBoundsConflictException,
                $"The losing operation must surface a compatibility conflict, but was {loser.Error?.GetType().Name}: {loser.Error?.Message}");

            await AssertCompatibleStateAsync(fixture, scenario);
            await fixture.ResetAsync();
        }
    }

    [OperationPolicyPostgresFact, Trait("Category", "Postgres")]
    public async Task OwnerPublishSerializedFirst_AdminActivationObservesCommittedSitePolicyAndConflicts()
    {
        await using var fixture = await TempDatabase.CreateAsync();
        var scenario = await SeedAsync(fixture);

        // Hold the shared gate, queue the Owner publish behind it, prove it is really blocked on
        // the PostgreSQL advisory lock, then let it run to completion.
        await using var holder = new NpgsqlConnection(fixture.ConnectionString);
        await holder.OpenAsync();
        await using var holdTransaction = await holder.BeginTransactionAsync();
        await PolicyCompatibility.AcquireOnConnectionAsync(holder, CancellationToken.None);

        var ownerTask = Task.Run(() => TryPublishAsync(fixture.ConnectionString, scenario));
        await WaitForAdvisoryWaitersAsync(fixture.ConnectionString, expected: 1, TimeSpan.FromSeconds(10));
        await holdTransaction.RollbackAsync();

        var owner = await ownerTask;
        Assert.True(owner.Won, $"Owner publish unexpectedly failed: {owner.Error}");

        // The platform activation now runs in the same serialized protocol. It must observe the
        // committed site policy and reject the incompatible platform bounds.
        var admin = await TryActivateAsync(fixture.ConnectionString, scenario);
        Assert.False(admin.Won);
        Assert.IsType<PolicyBoundsConflictException>(admin.Error);

        await using var db = fixture.Open();
        Assert.Equal(1, await db.Set<OperationalPolicyRow>()
            .CountAsync(x => x.SiteId == scenario.SiteId && x.IsActive));
        Assert.Equal(0, await db.Set<OperationalPolicyRow>()
            .CountAsync(x => x.Id == scenario.PlatformDraftId && x.IsActive));
    }

    [OperationPolicyPostgresFact, Trait("Category", "Postgres")]
    public async Task AdminActivationSerializedFirst_OwnerPublishObservesCommittedPlatformBoundsAndConflicts()
    {
        await using var fixture = await TempDatabase.CreateAsync();
        var scenario = await SeedAsync(fixture);

        await using var holder = new NpgsqlConnection(fixture.ConnectionString);
        await holder.OpenAsync();
        await using var holdTransaction = await holder.BeginTransactionAsync();
        await PolicyCompatibility.AcquireOnConnectionAsync(holder, CancellationToken.None);

        var adminTask = Task.Run(() => TryActivateAsync(fixture.ConnectionString, scenario));
        await WaitForAdvisoryWaitersAsync(fixture.ConnectionString, expected: 1, TimeSpan.FromSeconds(10));
        await holdTransaction.RollbackAsync();

        var admin = await adminTask;
        Assert.True(admin.Won, $"Admin activation unexpectedly failed: {admin.Error}");

        // The Owner publish now runs against the committed tighter platform revision and must
        // reject the value that was valid under the previous platform bounds.
        var owner = await TryPublishAsync(fixture.ConnectionString, scenario);
        Assert.False(owner.Won);
        Assert.IsType<PolicyOutOfBoundsException>(owner.Error);

        await AssertCompatibleStateAsync(fixture, scenario);
    }

    private static async Task AssertCompatibleStateAsync(TempDatabase fixture, Scenario scenario)
    {
        await using var db = fixture.Open();

        // No active site policy hosted by the scenario may exceed the bounds of the active
        // platform revision. This is the invariant the two-connection race must never break.
        var incompatible = await db.Database.SqlQuery<long>($"""
            SELECT count(*)::bigint AS "Value"
            FROM operational_policies p
            JOIN operational_policies s
              ON s.site_id = {scenario.SiteId}
             AND s.is_active
             AND (s.effective_until IS NULL OR now() < s.effective_until)
            WHERE p.tenant_id IS NULL AND p.site_id IS NULL AND p.is_active
              AND (p.effective_until IS NULL OR now() < p.effective_until)
              AND (p.override_bounds->'RESERVATION_MIN_DURATION'->>'Maximum')::int
                  < s.min_reservation_duration_minutes
            """).SingleAsync();
        Assert.Equal(0L, incompatible);

        var platformDraftActive = await db.Set<OperationalPolicyRow>()
            .AnyAsync(x => x.Id == scenario.PlatformDraftId && x.IsActive);
        var activeSitePolicies = await db.Set<OperationalPolicyRow>()
            .CountAsync(x => x.SiteId == scenario.SiteId && x.IsActive);

        // If the platform activation won, the Owner publish rolled back completely. If the Owner
        // publish won, the platform activation rolled back and the site policy stands alone.
        if (platformDraftActive)
            Assert.Equal(0, activeSitePolicies);
        else
            Assert.Equal(1, activeSitePolicies);
    }

    private static async Task<Scenario> SeedAsync(TempDatabase fixture)
    {
        await using var db = fixture.Open();
        var now = DateTimeOffset.UtcNow;

        var platformV1 = PlatformRow(now.AddHours(-2), version: 1, isActive: true, effectiveUntil: null,
            WideBounds);
        var platformV2Draft = PlatformRow(now.AddMinutes(30), version: 2, isActive: false, effectiveUntil: null,
            TightBounds);

        db.Set<OperationalPolicyRow>().AddRange(platformV1, platformV2Draft);
        await db.SaveChangesAsync();

        var scenario = new Scenario(platformV2Draft.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var repository = new LotPolicyRepository(db);
        await repository.SaveDraftAsync(scenario.SiteId, scenario.TenantId, scenario.ActorId,
            new PolicyDraftConfig { MinReservationDurationMinutes = 90 }, null, CancellationToken.None);
        return scenario;
    }

    private static OperationalPolicyRow PlatformRow(DateTimeOffset effectiveFrom, int version, bool isActive,
        DateTimeOffset? effectiveUntil, string overrideBounds) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = null,
        SiteId = null,
        Version = version,
        IsActive = isActive,
        CreatedAt = effectiveFrom,
        UpdatedAt = effectiveFrom,
        EffectiveFrom = effectiveFrom,
        EffectiveUntil = effectiveUntil,
        PaymentHoldDurationMinutes = 15,
        MinReservationDurationMinutes = 15,
        MaxReservationDurationHours = 24,
        MaxAdvanceBookingDays = 7,
        InterBookingBufferMinutes = 0,
        ReservationProtectionWindowHours = 4,
        AllocationLeadTimeMinutes = 60,
        FacilityFailureRefundLeadMinutes = 15,
        OccupancyValidityTimespanHours = 4,
        ArrivalLateToleranceMinutes = 15,
        // Platform-scope rows must leave the site-only flag NULL (database CHECK).
        ArrivalLateToleranceEnabled = null,
        RapidCancellationCount = 2,
        RapidCancellationWindowMinutes = 15,
        RapidCancellationRestrictionMinutes = 180,
        DailyCancellationLimit = 5,
        CancellationTimezone = "Asia/Ho_Chi_Minh",
        OverrideBounds = overrideBounds
    };

    private static async Task<Outcome> TryPublishAsync(string connectionString, Scenario scenario)
    {
        await using var db = Open(connectionString);
        var repository = new LotPolicyRepository(db);
        try
        {
            await repository.PublishDraftAsync(scenario.SiteId, scenario.TenantId, scenario.ActorId,
                "IMMEDIATE", null, "concurrent publish", null, CancellationToken.None);
            return new Outcome(true, null);
        }
        catch (Exception exception)
        {
            return new Outcome(false, exception);
        }
    }

    private static async Task<Outcome> TryActivateAsync(string connectionString, Scenario scenario)
    {
        await using var db = Open(connectionString);
        var repository = new ReservationPolicyRepository(db, TimeProvider.System);
        try
        {
            await repository.ActivateRevisionAsync(scenario.PlatformDraftId, DateTimeOffset.UtcNow,
                CancellationToken.None);
            return new Outcome(true, null);
        }
        catch (Exception exception)
        {
            return new Outcome(false, exception);
        }
    }

    private static ReservationDbContext Open(string connectionString) =>
        new(new DbContextOptionsBuilder<ReservationDbContext>().UseNpgsql(connectionString).Options);

    private static async Task WaitForAdvisoryWaitersAsync(string connectionString, int expected, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        await using var db = Open(connectionString);
        while (true)
        {
            var waiting = await db.Database.SqlQueryRaw<long>("""
                SELECT count(*)::bigint AS "Value"
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND wait_event_type = 'Lock'
                  AND query ILIKE '%pg_advisory_xact_lock%'
                """).SingleAsync(deadline.Token);
            if (waiting >= expected) return;
            await Task.Delay(20, deadline.Token);
        }
    }

    private sealed record Scenario(Guid PlatformDraftId, Guid SiteId, Guid TenantId, Guid ActorId);

    private sealed record Outcome(bool Won, Exception? Error);

    private sealed class TempDatabase : IAsyncDisposable
    {
        private readonly string adminConnection;
        private readonly string name = "smartpark_policy_test_" + Guid.NewGuid().ToString("N");

        public string ConnectionString { get; }

        private TempDatabase(string admin)
        {
            adminConnection = admin;
            ConnectionString = new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
        }

        public ReservationDbContext Open() => PlatformPolicyPostgresConcurrencyTests.Open(ConnectionString);

        public async Task ResetAsync()
        {
            await using var db = Open();
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE operational_policies, operational_policy_drafts");
        }

        public static async Task<TempDatabase> CreateAsync()
        {
            var fixture = new TempDatabase(
                Environment.GetEnvironmentVariable("SMARTPARK_OPERATION_POLICY_TEST_CONNECTION")
                ?? throw new InvalidOperationException("Configure the isolated PostgreSQL test server."));

            await using var admin = new NpgsqlConnection(fixture.adminConnection);
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{fixture.name}\"", admin).ExecuteNonQueryAsync();
            try
            {
                var root = FindRepositoryRoot();
                var schema = await File.ReadAllTextAsync(Path.Combine(root,
                    "scripts", "database", "microservices", "03b-reservation-operations-service-db.sql"));
                await using var db = fixture.Open();
                await db.Database.ExecuteSqlRawAsync(schema);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Only the generated database is ever dropped; existence and name are asserted first.
            Assert.Matches("^smartpark_policy_test_[0-9a-f]{32}$", name);
            using (var pool = new NpgsqlConnection(ConnectionString))
                NpgsqlConnection.ClearPool(pool);

            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin)
                .ExecuteNonQueryAsync();
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
    }
}