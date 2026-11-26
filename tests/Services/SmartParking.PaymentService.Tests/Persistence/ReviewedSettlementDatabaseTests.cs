using System.Security.Cryptography;
using System.Text.Json;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Outbox;
using SmartParking.PaymentService.Persistence.Repositories;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Persistence;

public sealed class ReviewedSettlementDatabaseTests
{
    private static readonly DateTimeOffset TransferTime = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    [ReviewedOrderDatabaseFact]
    public async Task OutboxAndCollectionRollBackTogetherAndAdditionalOnlyCreatesDisposition()
    {
        await using var db = Open();
        var order = await AddOrderAsync(db, linked: true);
        var checkout = Assert.Single(order.Transactions);
        var first = Guid.NewGuid().ToString("N");
        var extra = Guid.NewGuid().ToString("N");
        var fault = new SettlementCommitFault();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(ConnectionString).AddInterceptors(fault));
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddMassTransit(registration =>
        {
            registration.AddEntityFrameworkOutbox<PaymentDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox(bus => bus.DisableDeliveryService());
            });
            registration.UsingInMemory();
        });
        await using var provider = services.BuildServiceProvider();

        async Task<ProcessPayOsWebhookResult> CallbackAsync(string reference)
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = CreateHandler(scope.ServiceProvider.GetRequiredService<PaymentDbContext>(), order, reference,
                scope.ServiceProvider.GetRequiredService<IEventPublisher>());
            return await handler.Handle(new("{}", "signature"), default);
        }

        var messages = db.Set<OutboxMessage>().AsNoTracking().Where(message => message.Body.Contains(order.Id.ToString()));
        fault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => CallbackAsync(first));
        Assert.Empty(await messages.ToListAsync());
        Assert.Equal(PaymentOrderStatus.PENDING,
            (await db.PaymentOrders.AsNoTracking().SingleAsync(row => row.Id == order.Id)).Status);
        Assert.Null((await db.PaymentTransactions.AsNoTracking().SingleAsync(tx => tx.Id == checkout.Id)).ProviderCollectionReference);

        fault.Enabled = false;
        Assert.True((await CallbackAsync(first)).Success);
        Assert.True((await CallbackAsync(first)).Success);
        fault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => CallbackAsync(extra));
        Assert.False(await db.RefundRequests.AnyAsync(row => row.PaymentOrderId == order.Id));
        Assert.False(await db.PaymentTransactions.AnyAsync(tx => tx.ProviderCollectionReference == extra));

        fault.Enabled = false;
        Assert.True((await CallbackAsync(extra)).Success);
        Assert.True((await CallbackAsync(extra)).Success);
        var additional = await db.PaymentTransactions.SingleAsync(tx => tx.ProviderCollectionReference == extra);
        var request = await db.RefundRequests.SingleAsync(row => row.PaymentOrderId == order.Id);
        Assert.Equal(additional.Id, request.CollectionId);
        var message = Assert.Single(await messages.ToListAsync());
        using var envelope = JsonDocument.Parse(message.Body);
        var payload = envelope.RootElement.GetProperty("message");
        Assert.Equal(checkout.Id, payload.GetProperty("eventId").GetGuid());
        Assert.Equal(checkout.Id, payload.GetProperty("paymentTransactionId").GetGuid());
    }

    [ReviewedOrderDatabaseFact]
    public async Task ConcurrentSameLinkCallbacksCommitOneSettlementAndOneDisposition()
    {
        await using var db = Open();
        var order = await AddOrderAsync(db, linked: true);
        var checkout = Assert.Single(order.Transactions);
        var first = Guid.NewGuid().ToString("N");
        var additional = Guid.NewGuid().ToString("N");

        var results = await Task.WhenAll(DeliverAsync(order, first), DeliverAsync(order, additional), DeliverAsync(order, first));

        Assert.All(results, result => Assert.True(result.Result.Success));
        var settlement = Assert.Single(results.SelectMany(result => result.Events));
        Assert.Equal(checkout.Id, settlement.EventId);
        Assert.Equal("REVIEWED_RESERVATION_ORDER_PAID", settlement.SettlementClassification);
        Assert.Equal(checkout.Id, settlement.PaymentTransactionId);
        var stored = await db.PaymentOrders.AsNoTracking().Include(row => row.Transactions).SingleAsync(row => row.Id == order.Id);
        Assert.Equal(PaymentOrderStatus.PAID, stored.Status);
        Assert.Equal(2, stored.Transactions.Count);
        Assert.All(stored.Transactions, tx => Assert.True(tx.HasVerifiedProviderCollection()));
        var extra = Assert.Single(stored.Transactions, tx => tx.Id != checkout.Id);
        Assert.Null(extra.GatewayTransactionId);
        var request = await db.RefundRequests.SingleAsync(row => row.PaymentOrderId == order.Id);
        Assert.Equal((extra.Id, "REFUND_REQUESTED"), (request.CollectionId!.Value, request.Status));
    }

    [ReviewedOrderDatabaseFact]
    public async Task CheckoutResultAndStaleWriteCannotBypassOrOverwriteVerifiedEvidence()
    {
        await using var db = Open();
        var order = await AddOrderAsync(db, linked: false);
        var checkout = Assert.Single(order.Transactions);
        var reference = Guid.NewGuid().ToString("N");
        await using (var checkoutWrite = await db.Database.BeginTransactionAsync())
        {
            checkout.GatewayTransactionId = LinkFor(order);
            await db.SaveChangesAsync();

            var early = await DeliverAsync(order, reference);
            Assert.True(early.Result.Retryable);
            Assert.Empty(early.Events);
            await checkoutWrite.CommitAsync();
        }
        await using var competingCheckout = Open();
        var stale = await competingCheckout.PaymentTransactions.SingleAsync(tx => tx.Id == checkout.Id);

        Assert.True((await DeliverAsync(order, reference)).Result.Success);
        stale.MarkUnknown();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => competingCheckout.SaveChangesAsync());

        var accepted = await db.PaymentTransactions.AsNoTracking().SingleAsync(tx => tx.Id == checkout.Id);
        Assert.True(accepted.HasVerifiedProviderCollection());
        Assert.Equal(reference, accepted.ProviderCollectionReference);
        Assert.Equal(LinkFor(order), accepted.GatewayTransactionId);
    }

    [ReviewedOrderDatabaseFact]
    public async Task CrossOrderReferenceRaceNeverSettlesBothOrders()
    {
        await using var db = Open();
        var first = await AddOrderAsync(db, linked: true);
        var second = await AddOrderAsync(db, linked: true);
        var reference = Guid.NewGuid().ToString("N");

        async Task<bool> TryDeliverAsync(PaymentOrder order)
        {
            try
            {
                var result = await DeliverAsync(order, reference);
                if (!result.Result.Success)
                    Assert.Equal("COLLECTION_BINDING_CONFLICT", result.Result.ErrorCode);
                return result.Result.Success;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "idx_provider_collection_reference" })
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryDeliverAsync(first), TryDeliverAsync(second));
        Assert.Single(results, accepted => accepted);
        var rejected = results[0] ? second : first;
        Assert.Equal("COLLECTION_BINDING_CONFLICT", (await DeliverAsync(rejected, reference)).Result.ErrorCode);
        Assert.Equal(PaymentOrderStatus.PENDING, (await db.PaymentOrders.AsNoTracking().SingleAsync(row => row.Id == rejected.Id)).Status);
        Assert.Equal(1, await db.PaymentTransactions.CountAsync(tx => tx.ProviderCollectionReference == reference));
    }

    private static string ConnectionString => Environment.GetEnvironmentVariable("SMARTPARK_PAYMENT_ORDER_TEST_DB")!;

    private static PaymentDbContext Open() => new(new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(ConnectionString).Options);

    private static async Task<PaymentOrder> AddOrderAsync(PaymentDbContext db, bool linked)
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), SiteId = Guid.NewGuid(), AccountId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(), QuoteId = Guid.NewGuid(),
            OrderCode = RandomNumberGenerator.GetInt32(100_000_000, int.MaxValue).ToString(),
            IdempotencyKey = Guid.NewGuid().ToString(), OrderType = OrderType.RESERVATION_FEE,
            ReferenceType = ReferenceType.RESERVATION, ReviewedMethod = "PAYOS", ReviewedCurrency = "VND",
            Amount = 25_000, FinalAmount = 25_000, ExpiresAt = TransferTime.AddMinutes(-1)
        };
        order.Transactions.Add(new PaymentTransaction
        {
            Id = Guid.NewGuid(), TenantId = order.TenantId, PaymentOrderId = order.Id,
            GatewayTransactionId = linked ? LinkFor(order) : null, Amount = order.FinalAmount
        });
        await db.PaymentOrders.AddAsync(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static string LinkFor(PaymentOrder order) => $"PAYOS-{order.Id:N}";

    private static async Task<(ProcessPayOsWebhookResult Result, PaymentSucceededV1[] Events)> DeliverAsync(PaymentOrder order, string reference)
    {
        await using var db = Open();
        var events = new FakeEventPublisher();
        var result = await CreateHandler(db, order, reference, events).Handle(new("{}", "signature"), default);
        return (result, events.PublishedEvents.Cast<PaymentSucceededV1>().ToArray());
    }

    private static ProcessPayOsWebhookCommandHandler CreateHandler(PaymentDbContext db, PaymentOrder order, string reference, IEventPublisher events)
    {
        var gateway = new FakePaymentGatewayPort
        {
            CallbackResult = new(true, long.Parse(order.OrderCode), 25_000, LinkFor(order), reference,
                "SUCCESS", TransferTime, "{}", TransferTime)
        };
        return new ProcessPayOsWebhookCommandHandler(gateway, new ProviderWebhookRepository(db), new PaymentOrderRepository(db),
            new PaymentTransactionRepository(db), new InvoiceRepository(db), new RefundRequestRepository(db), events, new UnitOfWork(db),
            NullLogger<ProcessPayOsWebhookCommandHandler>.Instance);
    }
}
