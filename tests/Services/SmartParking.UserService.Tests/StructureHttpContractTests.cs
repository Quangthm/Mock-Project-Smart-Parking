using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using ReservationDomain = SmartParking.OperationService.Domain;

using OperationsUnitOfWorkTransaction = SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWorkTransaction;

namespace SmartParking.UserService.Tests;

public sealed class StructureHttpContractTests
{
    private const string ServiceKey = "structure-http-tests-service-key-only";

    [Fact]
    public async Task DashboardSnapshotUsesRealHttpContractWithoutCreatingAHold()
    {
        var store = new MemoryReservationStore();
        await using var host = CreateHost(store);
        await host.StartAsync();
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Service-Key", ServiceKey);
        var snapshot = await new StructureCommitmentsClient(client).ReadSnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(store.ProtectedSlot, Assert.Single(snapshot.ProtectedSlots));
        Assert.Null(store.CurrentToken);
        Assert.Equal(0, store.ReleaseCalls);
    }

    [Fact]
    public async Task PublicReservationRouteRequiresOperatorSessionAndInternalSnapshotRequiresServiceKey()
    {
        var store = new MemoryReservationStore();
        await using var host = OperationHost.Build([], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration["Services:Key"] = ServiceKey;
            builder.Configuration["ConnectionStrings:Reservation"] = "Host=localhost;Database=unused;Username=unused";
            builder.Services.AddSingleton<IStructureRepository>(store);
            builder.Services.AddSingleton<IOperationsConcurrency>(store);
            builder.Services.AddSingleton<SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWork>(store);
            builder.Services.AddSingleton<SmartParking.OperationService.Application.Reservations.IReservationFactsPublisher>(new NoopReservationFacts());
        });
        await host.StartAsync();
        using var client = host.GetTestClient();
        using var list = await client.GetAsync($"api/reservations?siteId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Contains("UNAUTHENTICATED", await list.Content.ReadAsStringAsync());
        using var sessions = await client.GetAsync($"api/parking-sessions?siteId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, sessions.StatusCode);
        Assert.Contains("UNAUTHENTICATED", await sessions.Content.ReadAsStringAsync());
        using var snapshot = await client.PostAsJsonAsync($"internal/structure/{Guid.NewGuid()}/snapshot", new { tenantId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, snapshot.StatusCode);
        Assert.Null(store.CurrentToken);
    }

    [Fact]
    public async Task RealAdapterRoundTripsTokenSnapshotAndIdempotentReleaseThroughApi()
    {
        var store = new MemoryReservationStore();
        await using var host = CreateHost(store);
        await host.StartAsync();
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Service-Key", ServiceKey);
        var adapter = new StructureCommitmentsClient(client);
        var tenant = Guid.NewGuid();
        var site = Guid.NewGuid();
        var token = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var outcome = new StructureOutcome(true, [removed], [], [new(null, "CAR", 3, 1)]);

        var snapshot = await adapter.AcquireAsync(tenant, site, token, CancellationToken.None);
        Assert.Equal(store.ProtectedSlot, Assert.Single(snapshot.ProtectedSlots));
        Assert.Equal(token, store.CurrentToken);
        await adapter.ReleaseAsync(tenant, site, token, outcome, CancellationToken.None);
        await adapter.ReleaseAsync(tenant, site, token, outcome, CancellationToken.None);

        Assert.Equal(1, store.AppliedOutcomes);
        Assert.Equal(removed, Assert.Single(store.LastOutcome!.RemovedSlots));
        Assert.Equal(1, store.LastOutcome.Capacities.Single().Backup);
    }

    [Fact]
    public async Task InvalidOutcomeReturns400BeforePersistenceIsCalled()
    {
        var store = new MemoryReservationStore();
        await using var host = CreateHost(store);
        await host.StartAsync();
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Service-Key", ServiceKey);

        using var response = await client.PostAsJsonAsync(
            $"internal/structure/{Guid.NewGuid()}/{Guid.NewGuid()}/release",
            new
            {
                tenantId = Guid.NewGuid(),
                outcome = new
                {
                    siteActive = true,
                    removedSlots = Array.Empty<Guid>(),
                    removedUnits = Array.Empty<Guid>(),
                    capacities = new[] { new { unitId = (Guid?)null, vehicleType = "CAR", capacity = 1, backup = 2 } }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_STRUCTURE", body.GetProperty("code").GetString());
        Assert.Equal(0, store.ReleaseCalls);
    }

    [Fact]
    public async Task HoldConflictReturns409AndAdapterMapsItToStructureBusy()
    {
        var store = new MemoryReservationStore { RejectAcquire = true };
        await using var host = CreateHost(store);
        await host.StartAsync();
        using var client = host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Service-Key", ServiceKey);
        var site = Guid.NewGuid();
        var token = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        using var response = await client.PutAsJsonAsync(
            $"internal/structure/{site}/{token}", new { tenantId = tenant });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            new StructureCommitmentsClient(client).AcquireAsync(tenant, site, token, CancellationToken.None));
        Assert.Equal("STRUCTURE_BUSY", error.Code);
    }

    [Fact]
    public async Task MissingServiceKeyReturns401AndAdapterMapsDeliveryFailure()
    {
        var store = new MemoryReservationStore();
        await using var host = CreateHost(store);
        await host.StartAsync();
        using var client = host.GetTestClient();
        using var response = await client.PostAsJsonAsync(
            $"internal/structure/{Guid.NewGuid()}/{Guid.NewGuid()}/release",
            new { tenantId = Guid.NewGuid(), outcome = (object?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            new StructureCommitmentsClient(client).ReleaseAsync(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None));
        Assert.Equal("COMMITMENTS_UNAVAILABLE", error.Code);
        Assert.Equal(0, store.ReleaseCalls);
    }

    private static WebApplication CreateHost(MemoryReservationStore store)
    {
        return OperationHost.Build([], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration["Services:Key"] = ServiceKey;
            builder.Configuration["ConnectionStrings:Reservation"] = "Host=localhost;Database=unused;Username=unused";
            builder.Services.AddSingleton<IStructureRepository>(store);
            builder.Services.AddSingleton<IOperationsConcurrency>(store);
            builder.Services.AddSingleton<SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWork>(store);
            builder.Services.AddSingleton<SmartParking.OperationService.Application.Reservations.IReservationFactsPublisher>(new NoopReservationFacts());
        });
    }

    // Only the SQL boundary is replaced. Requests still pass through the real
    // HTTP adapter, service-key middleware, JSON DTOs, Application and Domain.
    private sealed class MemoryReservationStore : IStructureRepository, IOperationsConcurrency,
        SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWork, OperationsUnitOfWorkTransaction
    {
        private readonly Dictionary<Guid, SmartParking.OperationService.Domain.Entities.StructureReleaseReceiptRow> receipts = [];
        private SmartParking.OperationService.Domain.Entities.StructureEditHoldRow? hold;
        private SmartParking.OperationService.Domain.Entities.StructureSiteStateRow? state;
        public Guid ProtectedSlot { get; } = Guid.NewGuid();
        public Guid? CurrentToken => hold?.Token;
        public bool RejectAcquire { get; set; }
        public int ReleaseCalls { get; private set; }
        public int AppliedOutcomes => receipts.Count;
        public ReservationDomain.EditOutcome? LastOutcome => receipts.Values.LastOrDefault() is { } row
            ? JsonSerializer.Deserialize<ReservationDomain.EditOutcome>(row.Outcome, new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
        public Task<ReservationDomain.HoldSnapshot> SnapshotAsync(Guid tenant, Guid site, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(new ReservationDomain.HoldSnapshot([ProtectedSlot], false, [new(null, "CAR", 0, 0)]));
        public Task<ReservationDomain.StructureImpactResponse> ImpactAsync(Guid tenant, Guid site, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(new ReservationDomain.StructureImpactResponse([], []));
        public Task<SmartParking.OperationService.Domain.Entities.StructureEditHoldRow?> HoldAsync(Guid site, CancellationToken ct)
        {
            if (RejectAcquire) throw new ReservationDomain.HoldConflictException();
            return Task.FromResult(hold);
        }
        public Task<SmartParking.OperationService.Domain.Entities.StructureReleaseReceiptRow?> ReceiptAsync(Guid token, CancellationToken ct) =>
            Task.FromResult(receipts.GetValueOrDefault(token));
        public Task<SmartParking.OperationService.Domain.Entities.StructureSiteStateRow?> StateAsync(Guid site, CancellationToken ct) => Task.FromResult(state);
        public Task<IReadOnlyList<SmartParking.OperationService.Domain.Entities.SiteCapacityPoolRow>> PoolsAsync(Guid tenant, Guid site, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SmartParking.OperationService.Domain.Entities.SiteCapacityPoolRow>>([]);
        public void AddHold(SmartParking.OperationService.Domain.Entities.StructureEditHoldRow row) => hold = row;
        public void RemoveHold(SmartParking.OperationService.Domain.Entities.StructureEditHoldRow row) { hold = null; ReleaseCalls++; }
        public void AddState(SmartParking.OperationService.Domain.Entities.StructureSiteStateRow row) => state = row;
        public void AddReceipt(SmartParking.OperationService.Domain.Entities.StructureReleaseReceiptRow row) => receipts.Add(row.Token, row);
        public Task LockSiteAsync(Guid site, CancellationToken ct) => Task.CompletedTask;
        public Task LockVehicleAsync(string identity, CancellationToken ct) => Task.CompletedTask;
        public IReservationRepository Reservations => throw new NotSupportedException();
        public IParkingSessionRepository ParkingSessions => throw new NotSupportedException();
        public ISlotAllocationRepository SlotAllocations => throw new NotSupportedException();
        public Task<OperationsUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<OperationsUnitOfWorkTransaction>(this);
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoopReservationFacts : SmartParking.OperationService.Application.Reservations.IReservationFactsPublisher
    {
        public Task AvailabilityChangedAsync(SmartParking.OperationService.Application.Reservations.ReservationAvailabilityChangedV1 fact, CancellationToken ct) => Task.CompletedTask;
        public Task FacilityAppliedAsync(SmartParking.OperationService.Application.Reservations.FacilityStructureAppliedV1 fact, CancellationToken ct) => Task.CompletedTask;
        public Task HoldChangedAsync(SmartParking.OperationService.Application.Reservations.ReservationHoldChangedV1 fact, CancellationToken ct) => Task.CompletedTask;
        public Task ConfirmedAsync(SmartParking.OperationService.Application.Reservations.ReservationConfirmedV1 fact, CancellationToken ct) => Task.CompletedTask;
    }
}