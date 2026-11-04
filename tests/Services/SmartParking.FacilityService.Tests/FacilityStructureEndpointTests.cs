using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.FacilityService.API;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.Tests;

// Route-level (HTTP contract) coverage for the SPARK-238 FacilityService structure surface.
// Everything below the HTTP boundary is replaced with in-process fakes, so the suite needs no
// Postgres, no Docker and no outbound call. The real Minimal-API routes, the real
// ParkingStructureService/StructureOperationService/MaintenanceWindowService and the real
// Program.cs middleware are exactly what run under test.
public sealed class FacilityStructureEndpointTests
{
    private const string ServiceKey = "facility-structure-endpoint-tests-service-key";
    private const string BearerToken = "test-bearer-token";

    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private static readonly Guid SiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LiveSlotId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ReleasedSlotId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid InactiveSiteId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid MaintenanceWindowId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid GuardingWindowId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ForeignResourceId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime ReleasedDeletedAt = new(2026, 10, 20, 9, 30, 0, DateTimeKind.Utc);

    private static readonly string[] RequiredStructureDataProperties =
    [
        "site", "units", "slots", "paths", "backupPolicies", "capacityViews", "serviceState"
    ];

    // 1. Auth: no bearer is rejected by the real middleware before any repository call.
    [Fact]
    public async Task ListWithoutBearerIs401Unauthenticated()
    {
        var harness = new Harness();
        using var factory = new FacilityApiFactory(harness);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/parking-lots");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertErrorEnvelopeAsync(response, "UNAUTHENTICATED");
        Assert.Equal(0, harness.Repository.ListCalls);
    }

    // 2. Create returns 201 with the structure Location header and the created site profile.
    [Fact]
    public async Task CreateSiteIs201WithStructureLocationAndProfile()
    {
        var harness = new Harness();
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.PostAsJsonAsync("/api/parking-lots", new
        {
            code = "site_new",
            name = "New Site",
            address = "9 New Road",
            latitude = (decimal?)null,
            longitude = (decimal?)null,
            tenantId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        var data = body.GetProperty("data");
        var id = data.GetProperty("id").GetGuid();
        Assert.Equal($"/api/parking-lots/{id}/structure", response.Headers.GetValues("Location").Single());
        Assert.Equal("SITE_NEW", data.GetProperty("code").GetString());
        Assert.Equal(TenantId, data.GetProperty("tenantId").GetGuid());
        Assert.True(data.GetProperty("isActive").GetBoolean());
    }

    // 3. A deactivated lot stays listed (Q4 / DE-2): the list is not active-filtered.
    [Fact]
    public async Task ListIncludesInactiveLotInDataArray()
    {
        var harness = new Harness();
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.GetAsync("/api/parking-lots");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        var data = body.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);

        var inactive = data.EnumerateArray()
            .Single(site => site.GetProperty("id").GetGuid() == InactiveSiteId);
        Assert.False(inactive.GetProperty("isActive").GetBoolean());
        Assert.Equal("INACTIVE", inactive.GetProperty("status").GetString());
        Assert.Contains(data.EnumerateArray(), site => site.GetProperty("id").GetGuid() == SiteId);
    }

    // 4. The structure read keeps the existing keys and adds serviceState additively.
    [Fact]
    public async Task StructureReadIncludesServiceStateAndExistingKeys()
    {
        var harness = new Harness();
        var now = DateTime.UtcNow;
        harness.Windows.Seed(new MaintenanceWindow(MaintenanceWindowId, TenantId, SiteId, null, null,
            now.AddHours(-1), now.AddHours(1), "Repaving", UserId, now.AddHours(-2), null, "pending", null, null));
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/structure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        var data = body.GetProperty("data");
        var names = data.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var required in RequiredStructureDataProperties) Assert.Contains(required, names);

        var serviceState = data.GetProperty("serviceState");
        Assert.Equal("MAINTENANCE_UNCERTAIN", serviceState.GetProperty("state").GetString());
        Assert.NotEqual(JsonValueKind.Null, serviceState.GetProperty("closedUntil").ValueKind);
        var windows = serviceState.GetProperty("windows");
        Assert.Equal(1, windows.GetArrayLength());
        Assert.Equal(MaintenanceWindowId, windows[0].GetProperty("id").GetGuid());
        Assert.Equal("pending", windows[0].GetProperty("enforcementState").GetString());
    }

    // 5a. A successful slot create returns 200 with the new identifier.
    [Fact]
    public async Task AddSlotIs200WithNewSlotId()
    {
        var harness = new Harness();
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.PostAsJsonAsync($"/api/parking-lots/{SiteId}/slots", new
        {
            unitId = UnitId,
            code = "A2",
            vehicleType = "CAR",
            type = "STANDARD",
            vehicleTypes = (string[]?)null,
            primaryVehicleType = (string?)null,
            tenantId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.NotEqual(Guid.Empty, body.GetProperty("data").GetGuid());
    }

    // 5b. A code owned by a soft-deleted row maps to 409 IDENTIFIER_RELEASED with the released block.
    [Fact]
    public async Task AddSlotWithReleasedCodeIs409IdentifierReleased()
    {
        var harness = new Harness();
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.PostAsJsonAsync($"/api/parking-lots/{SiteId}/slots", new
        {
            unitId = UnitId,
            code = "REL1",
            vehicleType = "CAR",
            type = "STANDARD",
            vehicleTypes = (string[]?)null,
            primaryVehicleType = (string?)null,
            tenantId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("IDENTIFIER_RELEASED", body.GetProperty("code").GetString());
        var released = body.GetProperty("released");
        Assert.Equal(ReleasedSlotId, released.GetProperty("id").GetGuid());
        Assert.Equal("REL1", released.GetProperty("code").GetString());
        Assert.Equal(new DateTimeOffset(ReleasedDeletedAt), released.GetProperty("deletedAt").GetDateTimeOffset());
    }

    // 6a. A representative structure action returns 200 {success:true}.
    [Fact]
    public async Task PatchActiveStructureActionIs200()
    {
        var harness = new Harness();
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await SendJsonAsync(client, HttpMethod.Patch,
            $"/api/parking-lots/{SiteId}/structure", EditBody("active", active: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
    }

    // 6b. The explicit restore action also returns 200 {success:true}.
    [Fact]
    public async Task PatchRestoreSlotStructureActionIs200()
    {
        var harness = new Harness();
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await SendJsonAsync(client, HttpMethod.Patch,
            $"/api/parking-lots/{SiteId}/structure", EditBody("restoreSlot", resourceId: ReleasedSlotId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
    }

    // 7a. A maintenance window that does not overlap is created as 201.
    [Fact]
    public async Task CreateMaintenanceWindowIs201()
    {
        var harness = new Harness();
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        var start = DateTime.UtcNow.AddHours(2);
        var end = DateTime.UtcNow.AddHours(3);
        using var response = await client.PostAsJsonAsync($"/api/parking-lots/{SiteId}/maintenance-windows",
            new { start, end, reason = "Repaving", tenantId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        var data = body.GetProperty("data");
        Assert.Equal("Repaving", data.GetProperty("reason").GetString());
        Assert.Equal("pending", data.GetProperty("enforcementState").GetString());
        Assert.Equal(SiteId, data.GetProperty("siteId").GetGuid());
        Assert.Equal($"/api/parking-lots/{SiteId}/maintenance-windows/{data.GetProperty("id").GetGuid()}",
            response.Headers.GetValues("Location").Single());
    }

    // 7b. An overlapping window maps to 409 STRUCTURE_IN_USE.
    [Fact]
    public async Task OverlappingMaintenanceWindowIs409StructureInUse()
    {
        var harness = new Harness();
        var now = DateTime.UtcNow;
        harness.Windows.Seed(new MaintenanceWindow(Guid.NewGuid(), TenantId, SiteId, null, null,
            now.AddHours(2), now.AddHours(4), "Repaving", UserId, now, null, "pending", null, null));
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.PostAsJsonAsync($"/api/parking-lots/{SiteId}/maintenance-windows",
            new { start = now.AddHours(3), end = now.AddHours(5), reason = "Overlap", tenantId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertErrorEnvelopeAsync(response, "STRUCTURE_IN_USE");
    }

    // 7c. A shortening PATCH violates extend-only and maps to 409.
    [Fact]
    public async Task ExtendMaintenanceWindowWithShorterEndIs409()
    {
        var harness = new Harness();
        var now = DateTime.UtcNow;
        harness.Windows.Seed(new MaintenanceWindow(MaintenanceWindowId, TenantId, SiteId, null, null,
            now.AddHours(2), now.AddHours(4), "Repaving", UserId, now, null, "pending", null, null));
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await SendJsonAsync(client, HttpMethod.Patch,
            $"/api/parking-lots/{SiteId}/maintenance-windows/{MaintenanceWindowId}",
            new { end = now.AddHours(3), tenantId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertErrorEnvelopeAsync(response, "STRUCTURE_IN_USE");
    }

    // 7d. Cancel is refused while another live window still covers now.
    [Fact]
    public async Task CancelMaintenanceWindowGuardedByAnotherLiveWindowIs409()
    {
        var harness = new Harness();
        var now = DateTime.UtcNow;
        harness.Windows.Seed(new MaintenanceWindow(MaintenanceWindowId, TenantId, SiteId, null, null,
            now.AddHours(-1), now.AddHours(1), "First", UserId, now.AddHours(-2), null, "pending", null, null));
        harness.Windows.Seed(new MaintenanceWindow(GuardingWindowId, TenantId, SiteId, null, null,
            now.AddMinutes(-30), now.AddHours(2), "Second", UserId, now.AddHours(-1), null, "pending", null, null));
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.DeleteAsync(
            $"/api/parking-lots/{SiteId}/maintenance-windows/{MaintenanceWindowId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertErrorEnvelopeAsync(response, "STRUCTURE_IN_USE");
    }

    // 8a. The batch route requires an Idempotency-Key header before any store call.
    [Fact]
    public async Task BatchWithoutIdempotencyKeyIs400()
    {
        var harness = new Harness();
        harness.Planner.PlanStatus = "resolved";
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.PostAsJsonAsync($"/api/parking-lots/{SiteId}/structure/batch", new
        {
            items = new[] { new { resourceId = SiteId, action = "active", expectedUpdatedAt = (DateTime?)null } },
            tenantId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertErrorEnvelopeAsync(response, "INVALID_STRUCTURE");
        Assert.Equal(0, harness.Operations.BeginCalls);
    }

    // 8b. One failing item yields 409 with the per-item failures and nothing applied.
    [Fact]
    public async Task BatchWithFailingItemIs409WithItems()
    {
        var harness = new Harness();
        harness.Planner.PlanStatus = "resolved";
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/parking-lots/{SiteId}/structure/batch")
        {
            Content = JsonContent.Create(new
            {
                items = new[]
                {
                    new { resourceId = ForeignResourceId, action = "removeSlot", expectedUpdatedAt = (DateTime?)null }
                },
                tenantId = (Guid?)null
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("STRUCTURE_CONFLICT", body.GetProperty("code").GetString());
        var items = body.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(ForeignResourceId, items[0].GetProperty("resourceId").GetGuid());
        Assert.Equal("NOT_FOUND", items[0].GetProperty("code").GetString());
    }

    // 8c. A fully valid batch completes through the operation route.
    [Fact]
    public async Task ValidBatchIs200()
    {
        var harness = new Harness();
        harness.Planner.PlanStatus = "resolved";
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/parking-lots/{SiteId}/structure/batch")
        {
            Content = JsonContent.Create(new
            {
                items = new[] { new { resourceId = SiteId, action = "active", expectedUpdatedAt = (DateTime?)null } },
                tenantId = (Guid?)null
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal("completed", body.GetProperty("data").GetProperty("status").GetString());
    }

    // 9a. A completed operation replays as 200 without re-planning.
    [Fact]
    public async Task OperationsCompletedIs200()
    {
        var harness = new Harness();
        harness.Operations.BeginStatus = "completed";
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.PostAsJsonAsync($"/api/parking-lots/{SiteId}/operations", new
        {
            idempotencyKey = Guid.NewGuid(),
            edit = EditBody("active", active: true),
            tenantId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal("completed", body.GetProperty("data").GetProperty("status").GetString());
    }

    // 9b. An unresolved plan reports 202 and does not claim completion.
    [Fact]
    public async Task OperationsPendingIs202()
    {
        var harness = new Harness();
        harness.Operations.BeginStatus = "pending";
        harness.Planner.PlanStatus = "pending";
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.PostAsJsonAsync($"/api/parking-lots/{SiteId}/operations", new
        {
            idempotencyKey = Guid.NewGuid(),
            edit = EditBody("active", active: true),
            tenantId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("pending", body.GetProperty("data").GetProperty("status").GetString());
    }

    // 9c. A failed plan reports 409.
    [Fact]
    public async Task OperationsConflictIs409()
    {
        var harness = new Harness();
        harness.Operations.BeginStatus = "pending";
        harness.Planner.PlanStatus = "failed";
        using var factory = new FacilityApiFactory(harness);
        using var client = Authenticated(factory);

        using var response = await client.PostAsJsonAsync($"/api/parking-lots/{SiteId}/operations", new
        {
            idempotencyKey = Guid.NewGuid(),
            edit = EditBody("active", active: true),
            tenantId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("failed", body.GetProperty("data").GetProperty("status").GetString());
    }

    private static HttpClient Authenticated(FacilityApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
        return client;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client, HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task AssertErrorEnvelopeAsync(HttpResponseMessage response, string expectedCode)
    {
        var body = await ReadJsonAsync(response);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(expectedCode, body.GetProperty("code").GetString());
    }

    // A full EditStructure payload. Every positional property is present so binding never depends
    // on System.Text.Json filling non-defaulted constructor parameters.
    private static object EditBody(string action, Guid resourceId = default, bool active = false) => new
    {
        action,
        resourceId,
        unitId = Guid.Empty,
        code = (string?)null,
        name = (string?)null,
        address = (string?)null,
        active,
        capacity = 0,
        vehicleType = (string?)null,
        slotType = "STANDARD",
        vehicleTypes = (string[]?)null,
        primaryVehicleType = (string?)null,
        coordinates3D = (string?)null,
        features = (string?)null,
        latitude = (decimal?)null,
        longitude = (decimal?)null,
        tenantId = (Guid?)null
    };

    private static SiteProfile ActiveSite => new(
        SiteId, TenantId, "SITE_01", "Site One", "1 Test Road", null, null, true, 1, "ACTIVE");

    private static SiteProfile InactiveSite => new(
        InactiveSiteId, TenantId, "SITE_02", "Site Two", "2 Test Road", null, null, false, 0, "INACTIVE");

    private static ParkingStructure BuildStructure()
    {
        var unit = new SpatialUnit(UnitId, null, $"/{SiteId}/{UnitId}/", UnitType.FLOOR, "Ground", 0);
        var live = new ParkingSlot(LiveSlotId, UnitId, "A1", VehicleType.CAR, SlotType.STANDARD);
        var released = new ParkingSlot(ReleasedSlotId, UnitId, "REL1", VehicleType.CAR, SlotType.STANDARD);
        return new ParkingStructure(
            ActiveSite, [unit], [live], [], null, false, null, null, [released], [], [], null);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Operations = new InMemoryStructureOperationStore();
            Synchronization = new InMemoryStructureSynchronizationStore(Operations);
            Repository = new InMemoryParkingStructureRepository(
                BuildStructure(),
                [ActiveSite, InactiveSite],
                Synchronization,
                new Dictionary<Guid, DateTime> { [ReleasedSlotId] = ReleasedDeletedAt });
            Commitments = new StubStructureCommitments();
            Planner = new StubStructureImpactPlanner();
            Windows = new InMemoryMaintenanceWindowStore();
            Owner = new StubOwnerAuthorizer();
            Operator = new StubSlotOperatorAuthorizer();
        }

        public InMemoryStructureOperationStore Operations { get; }
        public InMemoryStructureSynchronizationStore Synchronization { get; }
        public InMemoryParkingStructureRepository Repository { get; }
        public StubStructureCommitments Commitments { get; }
        public StubStructureImpactPlanner Planner { get; }
        public InMemoryMaintenanceWindowStore Windows { get; }
        public StubOwnerAuthorizer Owner { get; }
        public StubSlotOperatorAuthorizer Operator { get; }
    }

    private sealed class FacilityApiFactory(Harness harness) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Every outbound dependency points at a closed local port; the fakes mean a request
            // must not need them at all.
            builder.UseSetting("Services:Key", ServiceKey);
            builder.UseSetting("Services:User", "http://localhost:1/");
            builder.UseSetting("Services:Reservation", "http://localhost:1/");
            builder.UseSetting(
                "ConnectionStrings:Parking",
                "Host=localhost;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1;Pooling=false");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOwnerScopeReader>();
                services.AddSingleton<IOwnerScopeReader>(provider => new StubOwnerScopeReader(
                    provider.GetRequiredService<IHttpContextAccessor>(), UserId, TenantId));

                services.RemoveAll<IOwnerAuthorizer>();
                services.AddSingleton<IOwnerAuthorizer>(harness.Owner);

                services.RemoveAll<ISlotOperatorAuthorizer>();
                services.AddSingleton<ISlotOperatorAuthorizer>(harness.Operator);

                services.RemoveAll<IParkingStructureRepository>();
                services.AddSingleton<IParkingStructureRepository>(harness.Repository);

                services.RemoveAll<IStructureCommitments>();
                services.AddSingleton<IStructureCommitments>(harness.Commitments);

                services.RemoveAll<IStructureSynchronizationStore>();
                services.AddSingleton<IStructureSynchronizationStore>(harness.Synchronization);

                services.RemoveAll<IStructureOperationStore>();
                services.AddSingleton<IStructureOperationStore>(harness.Operations);

                services.RemoveAll<IStructureImpactPlanner>();
                services.AddSingleton<IStructureImpactPlanner>(harness.Planner);

                services.RemoveAll<IMaintenanceWindowStore>();
                services.AddSingleton<IMaintenanceWindowStore>(harness.Windows);

                // The background recovery worker would open the gate pool after 10s. Removing it
                // keeps startup and the whole test free of database I/O.
                var worker = services.FirstOrDefault(
                    descriptor => descriptor.ImplementationType == typeof(StructureSynchronizationWorker));
                if (worker is not null) services.Remove(worker);
            });
        }
    }

    // Mirrors OwnerIdentityClient.ScopeAsync's first check so the real middleware's 401 path is
    // exercised: no Authorization header is UNAUTHENTICATED before any outbound call.
    private sealed class StubOwnerScopeReader(IHttpContextAccessor accessor, Guid userId, Guid tenantId)
        : IOwnerScopeReader
    {
        public Task<IdentityScope> ScopeAsync(CancellationToken ct, string permission = "PARKING_MANAGE")
        {
            var bearer = accessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(bearer))
                throw new StructureException("UNAUTHENTICATED", "Sign in as an Owner.");
            return Task.FromResult(new IdentityScope(userId, [tenantId]));
        }
    }

    private sealed class StubOwnerAuthorizer : IOwnerAuthorizer
    {
        public Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class StubSlotOperatorAuthorizer : ISlotOperatorAuthorizer
    {
        public Task<OwnerScope> ScopeAsync(Guid site, CancellationToken cancellationToken)
            => Task.FromResult(new OwnerScope(UserId, TenantId));

        public Task<bool> IsAuthorizedAsync(OwnerScope actor, Guid site, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class StubStructureCommitments : IStructureCommitments
    {
        public Task<CommitmentSnapshot> ReadSnapshotAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken)
            => Task.FromResult(new CommitmentSnapshot([], false));

        public Task<CommitmentSnapshot> AcquireAsync(Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken)
            => Task.FromResult(new CommitmentSnapshot([], false));

        public Task ReleaseAsync(Guid tenantId, Guid siteId, Guid token, StructureOutcome? outcome, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ForceReleaseAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class InMemoryParkingStructureRepository(
        ParkingStructure structure,
        IReadOnlyList<SiteProfile> sites,
        InMemoryStructureSynchronizationStore synchronization,
        IReadOnlyDictionary<Guid, DateTime> releasedDeletedAt) : IParkingStructureRepository
    {
        private readonly List<SiteProfile> known = [.. sites];

        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken cancellationToken)
        {
            ListCalls++;
            return Task.FromResult<IReadOnlyList<SiteProfile>>(known.ToArray());
        }

        public Task<ParkingStructure> ReadAsync(
            OwnerScope owner, Guid siteId, CommitmentSnapshot? snapshot, CancellationToken cancellationToken)
            => siteId == structure.Site.Id
                ? Task.FromResult(structure)
                : Task.FromException<ParkingStructure>(
                    new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant."));

        public Task<SiteProfile> CreateAsync(OwnerScope owner, SiteProfile site, CancellationToken cancellationToken)
        {
            known.Add(site);
            return Task.FromResult(site);
        }

        public Task<IParkingStructureEdit> BeginEditAsync(
            OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, Guid? operationId, CancellationToken cancellationToken)
            => siteId == structure.Site.Id
                ? Task.FromResult<IParkingStructureEdit>(new Edit(structure, synchronization, releasedDeletedAt))
                : Task.FromException<IParkingStructureEdit>(
                    new StructureException("SITE_NOT_FOUND", "Parking site was not found in this tenant."));

        // Minimal faithful edit: the only observable write contract this suite needs is that a new
        // identifier colliding with a soft-deleted row is rejected with ReleasedIdentifierException,
        // and that a successful commit marks the synchronization row ready.
        private sealed class Edit(
            ParkingStructure structure,
            InMemoryStructureSynchronizationStore synchronization,
            IReadOnlyDictionary<Guid, DateTime> releasedDeletedAt) : IParkingStructureEdit
        {
            public ParkingStructure Structure => structure;

            public Task CommitAsync(
                Guid synchronizationToken, StructureOutcome outcome,
                Guid? markedSlot, string? reason, CancellationToken cancellationToken)
            {
                var released = structure.ReleasedSlots.FirstOrDefault(slot => structure.Slots.Any(
                    live => string.Equals(live.Code, slot.Code, StringComparison.OrdinalIgnoreCase)));
                if (released is not null)
                {
                    var deletedAt = releasedDeletedAt.TryGetValue(released.Id, out var value) ? value : DateTime.UtcNow;
                    throw new ReleasedIdentifierException(released.Id, released.Code, deletedAt);
                }

                synchronization.MarkReady(synchronizationToken);
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class InMemoryStructureSynchronizationStore(InMemoryStructureOperationStore operations)
        : IStructureSynchronizationStore
    {
        private readonly Dictionary<Guid, StructureSynchronization> rows = [];

        public Task<IAsyncDisposable?> TryLockSiteAsync(Guid siteId, CancellationToken cancellationToken)
            => Task.FromResult<IAsyncDisposable?>(new NoopDisposable());

        public Task PrepareAsync(StructureSynchronization synchronization, CancellationToken cancellationToken)
        {
            rows[synchronization.Token] = synchronization;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StructureSynchronization>> PendingAsync(Guid? siteId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StructureSynchronization>>(rows.Values
                .Where(row => row.State != "completed" && (siteId is null || row.SiteId == siteId))
                .ToArray());

        public Task<StructureSynchronization> FencePreparationAsync(Guid token, CancellationToken cancellationToken)
        {
            var row = rows[token] with { State = "aborting" };
            rows[token] = row;
            return Task.FromResult(row);
        }

        public Task AcknowledgeAsync(Guid token, CancellationToken cancellationToken)
        {
            if (rows.TryGetValue(token, out var row))
            {
                if (row.State == "ready" && row.OperationId is { } operationId)
                    operations.MarkCompleted(operationId);
                rows[token] = row with { State = "completed" };
            }

            return Task.CompletedTask;
        }

        // Called by the edit commit in place of the real UPDATE ... SET state='ready'.
        public void MarkReady(Guid token)
        {
            if (rows.TryGetValue(token, out var row)) rows[token] = row with { State = "ready" };
        }
    }

    private sealed class InMemoryStructureOperationStore : IStructureOperationStore
    {
        private readonly Dictionary<Guid, StructureOperation> operations = [];

        public string BeginStatus { get; set; } = "pending";
        public int BeginCalls { get; private set; }

        public Task<IAsyncDisposable> LockAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<IAsyncDisposable>(new NoopDisposable());

        public Task<StructureOperation> BeginAsync(
            OwnerScope owner, Guid site, Guid key, StructureEdit edit, CancellationToken cancellationToken)
        {
            BeginCalls++;
            var operation = new StructureOperation(Guid.NewGuid(), site, BeginStatus, "queued", null);
            operations[operation.Id] = operation;
            return Task.FromResult(operation);
        }

        public Task<StructureOperation> UpdateAsync(
            OwnerScope owner, Guid id, string status, string? reason, string? impact, CancellationToken cancellationToken)
        {
            var operation = operations.TryGetValue(id, out var existing)
                ? existing with { Status = status, Reason = reason, Impact = impact }
                : new StructureOperation(id, Guid.Empty, status, reason, impact);
            operations[id] = operation;
            return Task.FromResult(operation);
        }

        public Task<StructureOperation> ReadAsync(
            OwnerScope owner, Guid site, Guid id, CancellationToken cancellationToken)
            => operations.TryGetValue(id, out var operation)
                ? Task.FromResult(operation)
                : Task.FromException<StructureOperation>(
                    new StructureException("SITE_NOT_FOUND", "Operation was not found."));

        // Mirrors AcknowledgeAsync's completion of a committed, ready operation.
        public void MarkCompleted(Guid id)
        {
            if (operations.TryGetValue(id, out var operation))
                operations[id] = operation with { Status = "completed", Reason = null };
        }
    }

    private sealed class StubStructureImpactPlanner : IStructureImpactPlanner
    {
        public string PlanStatus { get; set; } = "resolved";

        public Task<ReallocationPlan> PlanAsync(
            OwnerScope owner, Guid siteId, Guid operationId, StructureEdit edit, CancellationToken cancellationToken)
            => Task.FromResult(new ReallocationPlan(PlanStatus, null, "none"));
    }

    // Replicates the Postgres store's observable rules: overlap exclusion on create, extend-only on
    // patch (delegated to the domain), and the reopen guard on cancel.
    private sealed class InMemoryMaintenanceWindowStore : IMaintenanceWindowStore
    {
        private readonly List<MaintenanceWindow> windows = [];

        public void Seed(MaintenanceWindow window) => windows.Add(window);

        public Task<IReadOnlyList<MaintenanceWindow>> ListAsync(
            OwnerScope owner, Guid siteId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MaintenanceWindow>>(windows
                .Where(window => window.CancelledAt is null)
                .OrderBy(window => window.Start)
                .ToArray());

        public Task<MaintenanceWindow> CreateAsync(
            OwnerScope owner, Guid siteId, DateTime start, DateTime end, string reason, CancellationToken cancellationToken)
        {
            if (windows.Any(window => window.CancelledAt is null && window.Start < end && start < window.End))
                throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window already covers this period.");

            var window = new MaintenanceWindow(
                Guid.NewGuid(), owner.TenantId, siteId, null, null, start, end, reason,
                owner.UserId, DateTime.UtcNow, null, "pending", null, null);
            windows.Add(window);
            return Task.FromResult(window);
        }

        public Task<MaintenanceWindow> ExtendAsync(
            OwnerScope owner, Guid siteId, Guid windowId, DateTime? start, DateTime? end, CancellationToken cancellationToken)
        {
            var index = windows.FindIndex(window => window.Id == windowId && window.CancelledAt is null);
            if (index < 0)
                throw new StructureException("STRUCTURE_CONFLICT", "Maintenance window was not found in this site.");

            var current = windows[index];
            var newEnd = MaintenanceWindowRules.Extend(current.Start, current.End, start, end, DateTime.UtcNow);
            var updated = current with
            {
                End = newEnd,
                EnforcementState = "pending",
                EnforcementRef = null,
                AcknowledgedAt = null
            };
            windows[index] = updated;
            return Task.FromResult(updated);
        }

        public Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken cancellationToken)
        {
            var index = windows.FindIndex(window => window.Id == windowId && window.CancelledAt is null);
            if (index < 0)
                throw new StructureException("STRUCTURE_CONFLICT", "Maintenance window was not found in this site.");

            var now = DateTime.UtcNow;
            if (windows.Any(window => window.Id != windowId && window.CancelledAt is null
                && window.Start <= now && now < window.End))
                throw new StructureException("STRUCTURE_IN_USE", "Another maintenance window still covers this period.");

            windows[index] = windows[index] with { CancelledAt = now };
            return Task.CompletedTask;
        }
    }

    private sealed class NoopDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
