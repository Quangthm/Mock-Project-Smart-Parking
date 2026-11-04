using System.Collections.Concurrent;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Usecase.CollectCashPayment;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Persistence.Outbox;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;
using Xunit.Abstractions;

namespace SmartParking.PaymentService.Tests.Integration;

[Trait("Category", "LiveBroker")]
public class RabbitMqLiveIntegrationTests
{
    private sealed class RabbitMqLiveFactAttribute : FactAttribute
    {
        public RabbitMqLiveFactAttribute()
        {
            if (!IsRabbitMqAvailable())
            {
                Skip = "Requires local RabbitMQ broker running on 127.0.0.1:5672. Skipping live broker test in CI/CD environment.";
            }
        }

        private static bool IsRabbitMqAvailable()
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var asyncResult = tcp.BeginConnect("127.0.0.1", 5672, null, null);
                var success = asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(200));
                if (!success) return false;
                tcp.EndConnect(asyncResult);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private readonly ITestOutputHelper _output;

    public RabbitMqLiveIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public class LiveMessageCollector
    {
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ConsumeContext<PaymentSucceededV1>>> _waiters = new();
        public readonly ConcurrentBag<ConsumeContext<PaymentSucceededV1>> ReceivedMessages = new();

        public Task<ConsumeContext<PaymentSucceededV1>> WaitForMessageAsync(Guid eventId, TimeSpan timeout)
        {
            var tcs = _waiters.GetOrAdd(eventId, _ => new TaskCompletionSource<ConsumeContext<PaymentSucceededV1>>(TaskCreationOptions.RunContinuationsAsynchronously));
            
            // Check if already received
            var existing = ReceivedMessages.FirstOrDefault(m => m.Message.EventId == eventId);
            if (existing != null)
            {
                tcs.TrySetResult(existing);
                return tcs.Task;
            }

            var timeoutCts = new CancellationTokenSource(timeout);
            timeoutCts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
            return tcs.Task;
        }

        public void RecordReceived(ConsumeContext<PaymentSucceededV1> context)
        {
            ReceivedMessages.Add(context);
            var tcs = _waiters.GetOrAdd(context.Message.EventId, _ => new TaskCompletionSource<ConsumeContext<PaymentSucceededV1>>(TaskCreationOptions.RunContinuationsAsynchronously));
            tcs.TrySetResult(context);
        }
    }

    public class DynamicPaymentSucceededConsumer : IConsumer<PaymentSucceededV1>
    {
        private readonly LiveMessageCollector _collector;
        private readonly ILogger<DynamicPaymentSucceededConsumer> _logger;

        public DynamicPaymentSucceededConsumer(LiveMessageCollector collector, ILogger<DynamicPaymentSucceededConsumer> logger)
        {
            _collector = collector;
            _logger = logger;
        }

        public Task Consume(ConsumeContext<PaymentSucceededV1> context)
        {
            _logger.LogInformation("Consumer received PaymentSucceededV1 EventId: {EventId}, CorrelationId: {CorrelationId}, Amount: {Amount}",
                context.Message.EventId, context.CorrelationId, context.Message.Amount);
            _collector.RecordReceived(context);
            return Task.CompletedTask;
        }
    }

    private ServiceProvider BuildServiceProvider(LiveMessageCollector collector, string queueName)
    {
        const string host = "127.0.0.1";
        const ushort port = 5672;
        const string vhost = "/";
        const string username = "guest";
        const string password = "guest";

        var services = new ServiceCollection();
        services.AddSingleton(collector);

        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddMassTransit(x =>
        {
            x.AddConsumer<DynamicPaymentSucceededConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(host, port, vhost, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ReceiveEndpoint(queueName, e =>
                {
                    e.ConfigureConsumer<DynamicPaymentSucceededConsumer>(context);
                });
            });
        });

        return services.BuildServiceProvider();
    }

    [RabbitMqLiveFact]
    public async Task LiveRabbitMq_PublishAndConsume_DirectOutboxPublisher_Succeeds()
    {
        var collector = new LiveMessageCollector();
        var queueName = "smartpark-direct-publish-" + Guid.NewGuid().ToString("N")[..8];
        var provider = BuildServiceProvider(collector, queueName);
        var busControl = provider.GetRequiredService<IBusControl>();

        _output.WriteLine($"[SETUP] Connecting to RabbitMQ at 127.0.0.1:5672, Queue: {queueName}...");
        await busControl.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

        try
        {
            var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var publisher = new OutboxEventPublisher(publishEndpoint, loggerFactory.CreateLogger<OutboxEventPublisher>());

            var eventPayload = new PaymentSucceededV1
            {
                EventId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = "ORDER-DIRECT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                PaymentOrderId = Guid.NewGuid(),
                PaymentTransactionId = Guid.NewGuid(),
                PaymentMethod = "PAYOS",
                GatewayReference = "PAYOS-REF-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                TenantId = Guid.NewGuid(),
                SiteId = Guid.NewGuid(),
                ReferenceType = "PARKING_SESSION",
                ReferenceId = Guid.NewGuid(),
                PlateNumber = "51A-888.99",
                AccountId = Guid.NewGuid(),
                Amount = 150000m,
                ParkingFee = 120000m,
                PenaltyFee = 30000m,
                PenaltyReason = "Overstay 1 hour",
                Currency = "VND"
            };

            _output.WriteLine($"[PUBLISH] Publishing EventId={eventPayload.EventId}, CorrelationId={eventPayload.CorrelationId}");
            await publisher.PublishAsync(eventPayload);

            var receivedContext = await collector.WaitForMessageAsync(eventPayload.EventId, TimeSpan.FromSeconds(15));
            Assert.NotNull(receivedContext);
            var received = receivedContext.Message;

            _output.WriteLine($"[CONSUME] Received EventId={received.EventId}, Amount={received.Amount}, Plate={received.PlateNumber}");
            Assert.Equal(eventPayload.EventId, received.EventId);
            Assert.Equal(eventPayload.CorrelationId, received.CorrelationId);
            Assert.Equal(eventPayload.Amount, received.Amount);
            Assert.Equal(eventPayload.PlateNumber, received.PlateNumber);
        }
        finally
        {
            await busControl.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
        }
    }

    [RabbitMqLiveFact]
    public async Task LiveRabbitMq_CollectCashPaymentCommandHandler_PublishesToBroker_ConsumerReceives()
    {
        var collector = new LiveMessageCollector();
        var queueName = "smartpark-cash-publish-" + Guid.NewGuid().ToString("N")[..8];
        var provider = BuildServiceProvider(collector, queueName);
        var busControl = provider.GetRequiredService<IBusControl>();

        _output.WriteLine($"[SETUP] Connecting to RabbitMQ at 127.0.0.1:5672, Queue: {queueName}...");
        await busControl.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

        try
        {
            var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var publisher = new OutboxEventPublisher(publishEndpoint, loggerFactory.CreateLogger<OutboxEventPublisher>());

            var orderId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var siteId = Guid.NewGuid();

            var orderRepo = new FakePaymentOrderRepository();
            var txRepo = new FakePaymentTransactionRepository();
            var invoiceRepo = new FakeInvoiceRepository();
            var uow = new FakeUnitOfWork();
            var actor = new FakeCurrentActor { Role = "OPERATOR", SiteId = siteId };

            var order = new PaymentOrder
            {
                Id = orderId,
                TenantId = tenantId,
                SiteId = siteId,
                OrderCode = "CASH-20261104-001",
                Status = PaymentOrderStatus.PENDING,
                FinalAmount = 85000,
                ReferenceId = sessionId,
                ReferenceType = ReferenceType.PARKING_SESSION
            };
            await orderRepo.AddAsync(order);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SiteId = siteId,
                PaymentOrderId = orderId,
                SessionId = sessionId,
                InvoiceNumber = "INV-20261104-0001",
                ReferenceType = ReferenceType.PARKING_SESSION,
                ReferenceId = sessionId,
                BaseParkingFee = 85000,
                OverstayPenaltyFee = 0,
                FinalAmount = 85000,
                PricingSnapshot = new InvoicePricingSnapshot
                {
                    Status = "PENDING",
                    PlateNumber = "29-G1 555.66",
                    SubtotalParkingFee = 85000,
                    TotalPenaltyFee = 0,
                    FinalTotalAmount = 85000
                }
            };
            await invoiceRepo.AddAsync(invoice);

            var handler = new CollectCashPaymentCommandHandler(
                orderRepo,
                txRepo,
                invoiceRepo,
                uow,
                publisher,
                actor,
                NullLogger<CollectCashPaymentCommandHandler>.Instance
            );

            _output.WriteLine($"[HANDLER] Executing CollectCashPaymentCommandHandler for Order {orderId}...");
            var result = await handler.Handle(new CollectCashPaymentCommand(orderId), CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("PAID", result.FinancialStatus.Status);

            _output.WriteLine("[AWAIT] Waiting for PaymentSucceededV1 on RabbitMQ queue...");
            // Await delivery via collector (finding by orderId)
            var deadline = DateTime.UtcNow.AddSeconds(15);
            ConsumeContext<PaymentSucceededV1>? matched = null;
            while (DateTime.UtcNow < deadline)
            {
                matched = collector.ReceivedMessages.FirstOrDefault(m => m.Message.PaymentOrderId == orderId);
                if (matched != null) break;
                await Task.Delay(100);
            }

            Assert.NotNull(matched);
            var msg = matched.Message;
            _output.WriteLine($"[SUCCESS] Consumer received event from RabbitMQ: TxId={msg.PaymentTransactionId}, Method={msg.PaymentMethod}, Amount={msg.Amount}, Plate={msg.PlateNumber}");
            Assert.Equal("CASH", msg.PaymentMethod);
            Assert.Equal(85000m, msg.Amount);
            Assert.Equal("29-G1 555.66", msg.PlateNumber);
            Assert.Equal(sessionId, msg.ReferenceId);
        }
        finally
        {
            await busControl.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
        }
    }

    [RabbitMqLiveFact]
    public async Task LiveRabbitMq_ProcessPayOsWebhookCommandHandler_PublishesToBroker_ConsumerReceives()
    {
        var collector = new LiveMessageCollector();
        var queueName = "smartpark-webhook-publish-" + Guid.NewGuid().ToString("N")[..8];
        var provider = BuildServiceProvider(collector, queueName);
        var busControl = provider.GetRequiredService<IBusControl>();

        _output.WriteLine($"[SETUP] Connecting to RabbitMQ at 127.0.0.1:5672, Queue: {queueName}...");
        await busControl.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

        try
        {
            var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var publisher = new OutboxEventPublisher(publishEndpoint, loggerFactory.CreateLogger<OutboxEventPublisher>());

            var orderId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var siteId = Guid.NewGuid();
            long orderCode = 987654;

            var gatewayPort = new FakePaymentGatewayPort
            {
                CallbackResult = new PaymentCallbackResult(
                    IsValidSignature: true,
                    OrderCode: orderCode,
                    AmountVnd: 50000,
                    GatewayTransactionId: "PAYOS-LIVE-TXN-987",
                    BankReference: "BANK-REF-987",
                    PaymentStatus: "SUCCESS",
                    TransactionTime: DateTimeOffset.UtcNow,
                    RawPayload: "{\"orderCode\":987654}"
                )
            };

            var webhookRepo = new FakeProviderWebhookRepository();
            var orderRepo = new FakePaymentOrderRepository();
            var txRepo = new FakePaymentTransactionRepository();
            var invoiceRepo = new FakeInvoiceRepository();
            var uow = new FakeUnitOfWork();

            var order = new PaymentOrder
            {
                Id = orderId,
                TenantId = tenantId,
                SiteId = siteId,
                OrderCode = orderCode.ToString(),
                Status = PaymentOrderStatus.PENDING,
                FinalAmount = 50000,
                ReferenceId = sessionId,
                ReferenceType = ReferenceType.PARKING_SESSION
            };
            await orderRepo.AddAsync(order);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SiteId = siteId,
                PaymentOrderId = orderId,
                SessionId = sessionId,
                InvoiceNumber = "INV-20261104-0002",
                ReferenceType = ReferenceType.PARKING_SESSION,
                ReferenceId = sessionId,
                BaseParkingFee = 50000,
                OverstayPenaltyFee = 0,
                FinalAmount = 50000,
                PricingSnapshot = new InvoicePricingSnapshot
                {
                    Status = "PENDING",
                    PlateNumber = "43-A1 777.88",
                    SubtotalParkingFee = 50000,
                    TotalPenaltyFee = 0,
                    FinalTotalAmount = 50000
                }
            };
            await invoiceRepo.AddAsync(invoice);

            var handler = new ProcessPayOsWebhookCommandHandler(
                gatewayPort,
                webhookRepo,
                orderRepo,
                txRepo,
                invoiceRepo,
                publisher,
                uow,
                NullLogger<ProcessPayOsWebhookCommandHandler>.Instance
            );

            _output.WriteLine($"[HANDLER] Executing ProcessPayOsWebhookCommandHandler for OrderCode {orderCode}...");
            var result = await handler.Handle(
                new ProcessPayOsWebhookCommand("{\"orderCode\":987654}", "valid_signature"),
                CancellationToken.None
            );

            Assert.NotNull(result);
            Assert.True(result.Success);

            _output.WriteLine("[AWAIT] Waiting for PaymentSucceededV1 on RabbitMQ queue...");
            var deadline = DateTime.UtcNow.AddSeconds(15);
            ConsumeContext<PaymentSucceededV1>? matched = null;
            while (DateTime.UtcNow < deadline)
            {
                matched = collector.ReceivedMessages.FirstOrDefault(m => m.Message.PaymentOrderId == orderId);
                if (matched != null) break;
                await Task.Delay(100);
            }

            Assert.NotNull(matched);
            var msg = matched.Message;
            _output.WriteLine($"[SUCCESS] Consumer received event from RabbitMQ: TxId={msg.PaymentTransactionId}, Method={msg.PaymentMethod}, Amount={msg.Amount}, Plate={msg.PlateNumber}, GatewayRef={msg.GatewayReference}");
            Assert.Equal("PAYOS", msg.PaymentMethod);
            Assert.Equal(50000m, msg.Amount);
            Assert.Equal("43-A1 777.88", msg.PlateNumber);
            Assert.Equal("PAYOS-LIVE-TXN-987", msg.GatewayReference);
        }
        finally
        {
            await busControl.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
        }
    }
}
