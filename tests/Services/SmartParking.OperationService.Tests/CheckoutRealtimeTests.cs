using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Usecase.ReservationGate;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Infrastructure.Messaging;
using SmartParking.OperationService.Infrastructure.Outbox;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using SmartParking.OperationService.Tests.TestDoubles;

namespace SmartParking.OperationService.Tests;

public class CheckoutRealtimeTests
{
    [Theory]
    [InlineData("SnapshotRequired", false, 0, 1)]
    [InlineData("SnapshotRequired", true, 0, 1)]
    [InlineData("OccupancyReleased", false, 1, 1)]
    [InlineData("OccupancyReleased", true, 1, 0)]
    public async Task Outbox_BroadcastsCheckoutRefreshWithoutPublishingItAsOccupancy_AndSuppressesDeltaEarlyBroadcastWhenRelayEnabled(
        string eventType,
        bool layoutRelayEnabled,
        int expectedBrokerCalls,
        int expectedBroadcasts)
    {
        var message = new OutboxMessage(Guid.NewGuid(), eventType, DateTimeOffset.UtcNow, 0, Guid.NewGuid(), "{}");
        var claimed = false;
        var brokerCalls = 0;
        var broadcasts = new List<OutboxMessage>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = Stub<IOccupancyOutboxStore>(name => {
            if (name == "MarkPublishedAsync") { completed.TrySetResult(); return Task.CompletedTask; }
            Assert.Equal("ClaimBatchAsync", name);
            IReadOnlyList<OutboxMessage> batch = claimed ? [] : [message];
            claimed = true;
            return Task.FromResult(batch);
        });
        using var provider = new ServiceCollection()
            .AddSingleton(store)
            .AddSingleton(Stub<IOutboxEventPublisher>(_ => { brokerCalls++; return Task.CompletedTask; }))
            .AddSingleton(Stub<IRealtimeLayoutBroadcaster>(_ => { broadcasts.Add(message); return Task.CompletedTask; }))
            .BuildServiceProvider();
        var workerConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:LayoutRelayEnabled"] = layoutRelayEnabled ? "true" : "false"
            })
            .Build();
        using var worker = new OccupancyOutboxWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OccupancyOutboxWorker>.Instance,
            workerConfig);
        await worker.StartAsync(CancellationToken.None);
        try { await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.Equal(expectedBroadcasts, broadcasts.Count);
        Assert.Equal(expectedBrokerCalls, brokerCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PaidDeparture_StagesRefreshInSameUnitOfWorkOnlyForMatchingOrder(bool matches)
    {
        var orderId = Guid.NewGuid();
        var session = new ParkingSessionRow { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), SiteId = Guid.NewGuid(),
            NormalizedPlate = "51A12345", EntryTime = DateTimeOffset.UtcNow.AddHours(-1), SessionStatus = "ACTIVE" };
        session.AwaitCheckoutPayment(orderId);
        var calls = new List<string>();
        using var db = new ReservationDbContext(new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var store = new OccupancyOutboxStore(db);
        var transaction = Stub<IUnitOfWorkTransaction>(name => {
            if (name == "CommitAsync") { calls.Add("commit"); return Task.CompletedTask; }
            return ValueTask.CompletedTask;
        });
        var work = Stub<IUnitOfWork>(name => {
            if (name == "BeginTransactionAsync") return Task.FromResult(transaction);
            Assert.Equal("SaveChangesAsync", name);
            Assert.Single(db.OutboxEvents.Local);
            Assert.Equal("COMPLETED", session.SessionStatus);
            calls.Add("save");
            return db.SaveChangesAsync();
        });
        var sessions = Stub<IParkingSessionRepository>(_ => Task.FromResult<ParkingSessionRow?>(session));
        var guards = Stub<IOperationsConcurrency>(_ => Task.CompletedTask);
        var allocations = Stub<ISlotAllocationRepository>(_ => Task.FromResult<IReadOnlyList<SlotAllocationRow>>([]));
        var handler = new SimulatePaidGateDepartureCommandHandler(sessions, Stub<IReservationRepository>(_ => throw new Exception()),
            allocations, guards, work, Stub<IOperatorGateSlotClaims>(_ => throw new Exception()), store);

        await handler.Handle(new(session.TenantId, session.SiteId, "PARKING_SESSION", session.Id,
            matches ? orderId : Guid.NewGuid(), DateTimeOffset.UtcNow), CancellationToken.None);

        if (!matches) { Assert.Empty(calls); Assert.Empty(db.OutboxEvents.Local); return; }
        Assert.Equal(new[] { "save", "commit" }, calls);
        var message = Assert.Single(await db.OutboxEvents.ToListAsync());
        Assert.Equal("SnapshotRequired", message.EventType);
        Assert.Equal(session.SiteId, message.SiteId);
        Assert.Contains("bookings", message.Payload, StringComparison.Ordinal);
        Assert.Null(message.PublishedAt);
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, false)]
    [InlineData(false, false, false, true, true, true, false)]
    [InlineData(false, false, true, false, true, false, true)]
    public void ReservationMessaging_ConfigurationMatrix_RegistersLayoutRelayIndependentlyOfPaymentCheckout(
        bool admission,
        bool audit,
        bool checkout,
        bool layoutRelay,
        bool expectedBus,
        bool expectedLayoutConsumer,
        bool expectedPaymentConsumer)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReservationAdmission:Enabled"] = admission ? "true" : "false",
                ["Messaging:AuditEnabled"] = audit ? "true" : "false",
                ["Messaging:Enabled"] = checkout ? "true" : "false",
                ["Messaging:LayoutRelayEnabled"] = layoutRelay ? "true" : "false",
                ["SMARTPARK_RABBITMQ_HOST"] = "127.0.0.1",
                ["SMARTPARK_RABBITMQ_PORT"] = "5672",
                ["SMARTPARK_RABBITMQ_VHOST"] = "smartpark",
                ["SMARTPARK_RABBITMQ_USER"] = "smartpark_test",
                ["SMARTPARK_RABBITMQ_PASSWORD"] = "not-a-real-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReservationMessaging(config);

        var hasBus = services.Any(s => s.ServiceType == typeof(IBusControl));
        var hasLayoutConsumer = services.Any(s => s.ServiceType == typeof(LayoutInvalidatedConsumer) || s.ImplementationType == typeof(LayoutInvalidatedConsumer));
        var hasPaymentConsumer = services.Any(s => s.ServiceType == typeof(PaymentSucceededConsumer) || s.ImplementationType == typeof(PaymentSucceededConsumer));
        var hasAdmissionWorker = services.Any(s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(ReservationAuthorizationWorker));

        Assert.Equal(expectedBus, hasBus);
        Assert.Equal(expectedLayoutConsumer, hasLayoutConsumer);
        Assert.Equal(expectedPaymentConsumer, hasPaymentConsumer);
        Assert.False(hasAdmissionWorker);
    }

    [Fact]
    public async Task LayoutInvalidatedConsumer_RelaysValidFacilityInvalidation_AndIgnoresUnknownSchemaOrResources()
    {
        var broadcasts = new List<OutboxMessage>();
        var capturingBroadcaster = new RecordingLayoutBroadcaster(broadcasts);
        var consumer = new LayoutInvalidatedConsumer(capturingBroadcaster);

        var siteId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        await consumer.Consume(CreateConsumeContext(new FacilityLayoutInvalidatedMessage(
            eventId,
            FacilityLayoutInvalidationTopology.CurrentSchemaVersion,
            occurredAt,
            tenantId,
            siteId,
            [FacilityLayoutInvalidationTopology.FullLayout, "bookings", FacilityLayoutInvalidationTopology.CapacitySummary])));

        var relayed = Assert.Single(broadcasts);
        Assert.Equal(eventId, relayed.EventId);
        Assert.Equal("SnapshotRequired", relayed.EventType);
        Assert.Equal(siteId, relayed.SiteId);
        Assert.Contains("full-layout", relayed.Payload, StringComparison.Ordinal);
        Assert.Contains("capacity-summary", relayed.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("bookings", relayed.Payload, StringComparison.Ordinal);

        broadcasts.Clear();

        // Unknown schema version, empty IDs, or non-whitelisted resources only must be ignored safely.
        await consumer.Consume(CreateConsumeContext(new FacilityLayoutInvalidatedMessage(
            Guid.NewGuid(), 99, occurredAt, tenantId, siteId, [FacilityLayoutInvalidationTopology.FullLayout])));
        await consumer.Consume(CreateConsumeContext(new FacilityLayoutInvalidatedMessage(
            Guid.NewGuid(), 1, occurredAt, Guid.Empty, siteId, [FacilityLayoutInvalidationTopology.FullLayout])));
        await consumer.Consume(CreateConsumeContext(new FacilityLayoutInvalidatedMessage(
            Guid.NewGuid(), 1, occurredAt, tenantId, Guid.Empty, [FacilityLayoutInvalidationTopology.FullLayout])));
        await consumer.Consume(CreateConsumeContext(new FacilityLayoutInvalidatedMessage(
            Guid.NewGuid(), 1, occurredAt, tenantId, siteId, ["bookings", "unknown"])));

        Assert.Empty(broadcasts);
    }

    private static ConsumeContext<FacilityLayoutInvalidatedMessage> CreateConsumeContext(FacilityLayoutInvalidatedMessage message) =>
        Stub<ConsumeContext<FacilityLayoutInvalidatedMessage>>(name => name switch
        {
            "get_Message" => message,
            "get_CancellationToken" => CancellationToken.None,
            _ => null
        });

    private static T Stub<T>(Func<string, object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, PortStub>();
        ((PortStub)(object)proxy).Call = call;
        return proxy;
    }
    public class PortStub : DispatchProxy
    {
        public Func<string, object?> Call { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!.Name);
    }
}
