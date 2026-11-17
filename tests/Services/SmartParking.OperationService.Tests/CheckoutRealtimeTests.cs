using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Usecase.ReservationGate;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;
using SmartParking.OperationService.Infrastructure.Outbox;

namespace SmartParking.OperationService.Tests;

public class CheckoutRealtimeTests
{
    [Theory]
    [InlineData("SnapshotRequired", 0)]
    [InlineData("OccupancyReleased", 1)]
    public async Task Outbox_BroadcastsCheckoutRefreshWithoutPublishingItAsOccupancy(string eventType, int expectedBrokerCalls)
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
        using var worker = new OccupancyOutboxWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OccupancyOutboxWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try { await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.Single(broadcasts);
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
        Assert.Null(message.PublishedAt);
    }

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
