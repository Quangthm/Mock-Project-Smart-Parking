using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.ParkingService.API;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Infrastructure;

namespace SmartParking.ParkingService.Tests;

// Route-level coverage for GET /api/parking-lots/{id}/operational-layout.
// Everything below the HTTP boundary (persistence, UserService, ReservationService)
// is replaced so the test needs no Postgres, Docker or outbound service.
public sealed class OperationalLayoutEndpointTests
{
    private const string ServiceKey = "operational-layout-endpoint-tests-service-key";
    private const string CacheControlValue = "no-store, no-cache, must-revalidate";

    private const string UnknownFloor = "no-such-floor";

    private static readonly Guid SiteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FloorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ZoneId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AvailableSlotId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid BackupSlotId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset GeneratedAt = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

    private static readonly string[] RequiredDataProperties =
    [
        "siteId", "siteCode", "siteName", "contractVersion", "generatedAt",
        "isFresh", "stalenessThresholdSeconds", "metrics", "floors", "slots"
    ];

    private static LayoutCaller TelemetryCaller => new(
        LayoutCallerKind.Operator,
        Guid.Parse("66666666-6666-6666-6666-666666666666"),
        [Guid.Parse("77777777-7777-7777-7777-777777777777")],
        IsSiteTelemetryGranted: true);

    private static LayoutCaller DriverCaller => new(
        LayoutCallerKind.Driver,
        Guid.Parse("88888888-8888-8888-8888-888888888888"),
        [],
        IsSiteTelemetryGranted: false);

    [Fact]
    public async Task ValidCallerGets200WithContractEnvelopeHeadersAndCoordinates3d()
    {
        var resolver = new StubCallerResolver(TelemetryCaller);
        var reader = new StubLayoutReader(TelemetryReadResult());
        var commitments = new RecordingCommitments();
        using var factory = new LayoutApiFactory(resolver, reader, commitments);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/operational-layout");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOperationalLayoutHeaders(response);

        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.True, root.GetProperty("success").ValueKind);

        var data = root.GetProperty("data");
        var actualProperties = data.EnumerateObject().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(RequiredDataProperties.OrderBy(name => name, StringComparer.Ordinal), actualProperties);

        Assert.Equal(SiteId, data.GetProperty("siteId").GetGuid());
        Assert.Equal("SITE_EDGE_01", data.GetProperty("siteCode").GetString());
        Assert.Equal("Edge Contract Site", data.GetProperty("siteName").GetString());
        Assert.Equal(OperationalLayoutContract.Version, data.GetProperty("contractVersion").GetString());
        Assert.Equal(GeneratedAt, data.GetProperty("generatedAt").GetDateTimeOffset());
        Assert.True(data.GetProperty("isFresh").GetBoolean());
        Assert.Equal(OperationalLayoutContract.StalenessThresholdSeconds, data.GetProperty("stalenessThresholdSeconds").GetInt32());

        var slots = data.GetProperty("slots");
        Assert.Equal(2, slots.GetArrayLength());

        var bookable = slots[0];
        Assert.Equal("AVAILABLE", bookable.GetProperty("physicalState").GetString());
        Assert.Equal("UNRESERVED", bookable.GetProperty("reservationState").GetString());
        Assert.True(bookable.GetProperty("isBookable").GetBoolean());
        Assert.Equal(3, bookable.GetProperty("coordinates3d").GetProperty("z").GetInt32());
        Assert.False(bookable.TryGetProperty("coordinates3D", out _));

        var backup = slots[1];
        Assert.Equal("BACKUP", backup.GetProperty("reservationState").GetString());
        Assert.False(backup.GetProperty("isBookable").GetBoolean());
    }

    [Fact]
    public async Task ResolverUnauthenticatedGets401WithCodeAndLayoutHeaders()
    {
        var resolver = new StubCallerResolver(new StructureException("UNAUTHENTICATED", "Session is invalid or revoked."));
        var reader = new StubLayoutReader(TelemetryReadResult());
        using var factory = new LayoutApiFactory(resolver, reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/operational-layout");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        await AssertErrorEnvelopeAsync(response, "UNAUTHENTICATED");
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task ResolverForbiddenGets403WithCodeAndLayoutHeaders()
    {
        var resolver = new StubCallerResolver(new StructureException("FORBIDDEN", "This role cannot access the operational layout."));
        var reader = new StubLayoutReader(TelemetryReadResult());
        using var factory = new LayoutApiFactory(resolver, reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/operational-layout");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        await AssertErrorEnvelopeAsync(response, "FORBIDDEN");
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task ReaderSiteNotFoundGets404WithCodeAndLayoutHeaders()
    {
        var reader = new StubLayoutReader(new StructureException("SITE_NOT_FOUND", "Parking site was not found."));
        using var factory = new LayoutApiFactory(new StubCallerResolver(DriverCaller), reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/operational-layout");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        await AssertErrorEnvelopeAsync(response, "SITE_NOT_FOUND");
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task ReaderIdentityUnavailableGets503WithCodeAndLayoutHeaders()
    {
        var reader = new StubLayoutReader(new StructureException("IDENTITY_UNAVAILABLE", "Identity service is unavailable."));
        using var factory = new LayoutApiFactory(new StubCallerResolver(DriverCaller), reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/operational-layout");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        await AssertErrorEnvelopeAsync(response, "IDENTITY_UNAVAILABLE");
    }

    [Fact]
    public async Task ReaderUnexpectedFailureGets500WithInternalErrorAndLayoutHeaders()
    {
        var reader = new StubLayoutReader(new InvalidOperationException("database exploded"));
        using var factory = new LayoutApiFactory(new StubCallerResolver(DriverCaller), reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/operational-layout");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertOperationalLayoutHeaders(response);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.False, body.GetProperty("success").ValueKind);
        Assert.Equal("INTERNAL_ERROR", body.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvalidVehicleTypeGets400BeforeResolvingOrReading()
    {
        var resolver = new StubCallerResolver(TelemetryCaller);
        var reader = new StubLayoutReader(TelemetryReadResult());
        using var factory = new LayoutApiFactory(resolver, reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/parking-lots/{SiteId}/operational-layout?vehicleType=BOGUS");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        await AssertErrorEnvelopeAsync(response, "INVALID_VEHICLE_TYPE");
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task NumericVehicleTypeGets400BeforeResolvingOrReading()
    {
        var resolver = new StubCallerResolver(TelemetryCaller);
        var reader = new StubLayoutReader(TelemetryReadResult());
        using var factory = new LayoutApiFactory(resolver, reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        // Enum.TryParse would accept "1" as MOTORCYCLE; the contract only allows names.
        using var response = await client.GetAsync(
            $"/api/parking-lots/{SiteId}/operational-layout?vehicleType=1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        await AssertErrorEnvelopeAsync(response, "INVALID_VEHICLE_TYPE");
        Assert.Equal(0, resolver.CallCount);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task CanonicalVehicleTypeStillGets200()
    {
        var resolver = new StubCallerResolver(TelemetryCaller);
        var reader = new StubLayoutReader(TelemetryReadResult());
        using var factory = new LayoutApiFactory(resolver, reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/parking-lots/{SiteId}/operational-layout?vehicleType=CAR");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.True, body.GetProperty("success").ValueKind);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task ReaderInvalidFloorGets400WithCodeAndLayoutHeaders()
    {
        var reader = new StubLayoutReader(new StructureException("INVALID_FLOOR", "The requested floor does not exist."));
        using var factory = new LayoutApiFactory(new StubCallerResolver(DriverCaller), reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/parking-lots/{SiteId}/operational-layout?floor={UnknownFloor}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        await AssertErrorEnvelopeAsync(response, "INVALID_FLOOR");
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(UnknownFloor, reader.LastFloor);
    }

    [Fact]
    public async Task StructureBusyRaisedByReaderIsMappedTo409ByTheSharedMiddleware()
    {
        var reader = new StubLayoutReader(new StructureException("STRUCTURE_BUSY", "Another request is managing this site."));
        using var factory = new LayoutApiFactory(new StubCallerResolver(DriverCaller), reader, new RecordingCommitments());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/operational-layout");

        // Verified route-level behaviour: the endpoint rethrows StructureException to
        // Program.cs's shared middleware, which maps STRUCTURE_BUSY to 409. The route does
        // not hide it as a 200/500. This pins the actual status so a future mapping change
        // is visible.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertOperationalLayoutHeaders(response);
        await AssertErrorEnvelopeAsync(response, "STRUCTURE_BUSY");
    }

    [Fact]
    public async Task ReadPathNeverCallsStructureCommitments()
    {
        var resolver = new StubCallerResolver(TelemetryCaller);
        var reader = new StubLayoutReader(TelemetryReadResult());
        var commitments = new RecordingCommitments();
        using var factory = new LayoutApiFactory(resolver, reader, commitments);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/parking-lots/{SiteId}/operational-layout");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal(0, commitments.ReadSnapshotCalls);
        Assert.Equal(0, commitments.AcquireCalls);
        Assert.Equal(0, commitments.ReleaseCalls);
    }

    private static void AssertOperationalLayoutHeaders(HttpResponseMessage response)
    {
        // NonValidated keeps the raw header text; the validated collection reparses and
        // reorders Cache-Control directives.
        Assert.True(response.Headers.NonValidated.TryGetValues("Cache-Control", out var cacheControl));
        Assert.Equal(CacheControlValue, cacheControl.ToString());

        var vary = response.Headers.NonValidated["Vary"];
        Assert.Equal("Authorization", vary.ToString());
    }

    private static async Task AssertErrorEnvelopeAsync(HttpResponseMessage response, string expectedCode)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.False, body.GetProperty("success").ValueKind);
        Assert.Equal(expectedCode, body.GetProperty("code").GetString());
    }

    private static OperationalLayoutReadResult TelemetryReadResult()
    {
        using var coordinates = JsonDocument.Parse("""{"x":1,"y":0,"z":3}""");

        var bookable = new SlotVisualStateDto(
            AvailableSlotId,
            "CarParkingSpace_001",
            FloorId,
            "Floor_G",
            "Zone_A",
            "Row_01",
            VehicleType.CAR,
            SlotType.STANDARD,
            PhysicalState.AVAILABLE,
            ReservationState.UNRESERVED,
            SessionType.STANDARD,
            IsEvCharging: false,
            IsBookable: true,
            Coordinates3D: coordinates.RootElement.Clone(),
            Features: null,
            OperatorContext: null,
            UpdatedAt: GeneratedAt);

        var backup = bookable with
        {
            SlotId = BackupSlotId,
            SlotCode = "CarParkingSpace_002",
            ReservationState = ReservationState.BACKUP,
            IsBookable = false
        };

        var layout = new OperationalLayoutProjectionDto(
            SiteId,
            "SITE_EDGE_01",
            "Edge Contract Site",
            OperationalLayoutContract.Version,
            GeneratedAt,
            IsFresh: true,
            OperationalLayoutContract.StalenessThresholdSeconds,
            new LayoutMetricsDto(
                TotalPhysicalCapacity: 2,
                OccupiedCount: 0,
                ReservedCount: 1,
                AvailableCarSlots: 1,
                new MotorcycleCapacityDto(0, 0, 0),
                [new ZoneCapacityDto(ZoneId, "Zone_A", "Floor_G", "North", 2, 0, 2, 1)]),
            [new FloorProjectionDto(FloorId, "Floor_G", "Ground", 1, 2)],
            [bookable, backup]);

        return new OperationalLayoutReadResult(
            layout,
            MaskedReservedCount: 1,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["Zone_A"] = 1 });
    }

    private sealed class LayoutApiFactory(
        ILayoutCallerResolver resolver,
        IOperationalLayoutReader reader,
        IStructureCommitments commitments) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Every outbound dependency points at a closed local port. The fakes below
            // mean a request must not need them at all.
            builder.UseSetting("Services:Key", ServiceKey);
            builder.UseSetting("Services:User", "http://localhost:1/");
            builder.UseSetting("Services:Reservation", "http://localhost:1/");
            builder.UseSetting(
                "ConnectionStrings:Parking",
                "Host=localhost;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1;Pooling=false");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILayoutCallerResolver>();
                services.AddSingleton(resolver);

                services.RemoveAll<IOperationalLayoutReader>();
                services.AddSingleton(reader);

                services.RemoveAll<IStructureCommitments>();
                services.AddSingleton(commitments);

                // The background recovery worker would open the gate pool after 10s.
                // Removing it keeps startup and the whole test free of database I/O.
                var worker = services.FirstOrDefault(
                    descriptor => descriptor.ImplementationType == typeof(StructureSynchronizationWorker));
                if (worker is not null) services.Remove(worker);
            });
        }
    }

    private sealed class StubCallerResolver : ILayoutCallerResolver
    {
        private readonly LayoutCaller? caller;
        private readonly Exception? error;

        public StubCallerResolver(LayoutCaller caller) => this.caller = caller;
        public StubCallerResolver(Exception error) => this.error = error;

        public int CallCount { get; private set; }

        public Task<LayoutCaller> ResolveAsync(Guid siteId, CancellationToken ct)
        {
            CallCount++;
            return error is null
                ? Task.FromResult(caller!)
                : Task.FromException<LayoutCaller>(error);
        }
    }

    private sealed class StubLayoutReader : IOperationalLayoutReader
    {
        private readonly OperationalLayoutReadResult? result;
        private readonly Exception? error;

        public StubLayoutReader(OperationalLayoutReadResult result) => this.result = result;
        public StubLayoutReader(Exception error) => this.error = error;

        public int CallCount { get; private set; }
        public string? LastFloor { get; private set; }

        public Task<OperationalLayoutReadResult> ReadAsync(
            LayoutReadScope scope,
            Guid siteId,
            VehicleType? vehicleType,
            string? floor,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastFloor = floor;
            return error is null
                ? Task.FromResult(result!)
                : Task.FromException<OperationalLayoutReadResult>(error);
        }
    }

    private sealed class RecordingCommitments : IStructureCommitments
    {
        public int ReadSnapshotCalls { get; private set; }
        public int AcquireCalls { get; private set; }
        public int ReleaseCalls { get; private set; }
        public int ForceReleaseCalls { get; private set; }

        public Task<CommitmentSnapshot> ReadSnapshotAsync(
            Guid tenantId, Guid siteId, CancellationToken cancellationToken)
        {
            ReadSnapshotCalls++;
            throw new InvalidOperationException(
                "The operational-layout read path must not read a structure commitment snapshot.");
        }

        public Task<CommitmentSnapshot> AcquireAsync(
            Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken)
        {
            AcquireCalls++;
            throw new InvalidOperationException(
                "The operational-layout read path must not acquire a structure commitment hold.");
        }

        public Task ReleaseAsync(
            Guid tenantId, Guid siteId, Guid token, StructureOutcome? outcome,
            CancellationToken cancellationToken)
        {
            ReleaseCalls++;
            throw new InvalidOperationException(
                "The operational-layout read path must not release a structure commitment hold.");
        }

        public Task ForceReleaseAsync(
            Guid tenantId, Guid siteId, CancellationToken cancellationToken)
        {
            ForceReleaseCalls++;
            throw new InvalidOperationException(
                "The operational-layout read path must not force-release a structure commitment hold.");
        }
    }
}
