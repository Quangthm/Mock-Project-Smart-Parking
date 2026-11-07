using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
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

namespace SmartParking.PaymentService.Tests.Infrastructure;

public class PaymentOutboxTests
{
    [Fact]
    public async Task OutboxPublisher_WhenEventPublished_ShouldBeReceivedByMassTransitHarness()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness();

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
            var publisher = new OutboxEventPublisher(publishEndpoint, NullLogger<OutboxEventPublisher>.Instance);

            var @event = new PaymentSucceededV1
            {
                EventId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                CorrelationId = "TEST-CORR-001",
                PaymentOrderId = Guid.NewGuid(),
                PaymentTransactionId = Guid.NewGuid(),
                PaymentMethod = "PAYOS",
                Amount = 50000m,
                ParkingFee = 50000m,
                Currency = "VND"
            };

            await publisher.PublishAsync(@event);

            Assert.True(await harness.Published.Any<PaymentSucceededV1>());
            var publishedMessage = harness.Published.Select<PaymentSucceededV1>().FirstOrDefault();
            Assert.NotNull(publishedMessage);
            Assert.Equal("TEST-CORR-001", publishedMessage!.Context.Message.CorrelationId);
            Assert.Equal(50000m, publishedMessage.Context.Message.Amount);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task CollectCashPayment_WhenExecuted_PublishesPaymentSucceededV1ToHarness()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness();

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
            var publisher = new OutboxEventPublisher(publishEndpoint, NullLogger<OutboxEventPublisher>.Instance);

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
                OrderCode = "20261102001",
                Status = PaymentOrderStatus.PENDING,
                FinalAmount = 65000,
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
                InvoiceNumber = "INV-20261102-0001",
                ReferenceType = ReferenceType.PARKING_SESSION,
                ReferenceId = sessionId,
                BaseParkingFee = 65000,
                OverstayPenaltyFee = 0,
                FinalAmount = 65000,
                PricingSnapshot = new InvoicePricingSnapshot
                {
                    Status = "PENDING",
                    PlateNumber = "59-A1 999.99",
                    SubtotalParkingFee = 65000,
                    TotalPenaltyFee = 0,
                    FinalTotalAmount = 65000
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

            var result = await handler.Handle(new CollectCashPaymentCommand(orderId), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("PAID", result.FinancialStatus.Status);

            Assert.True(await harness.Published.Any<PaymentSucceededV1>());
            var published = harness.Published.Select<PaymentSucceededV1>().FirstOrDefault()?.Context.Message;
            Assert.NotNull(published);
            Assert.Equal(orderId, published!.PaymentOrderId);
            Assert.Equal("CASH", published.PaymentMethod);
            Assert.Equal(65000m, published.Amount);
            Assert.Equal("59-A1 999.99", published.PlateNumber);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ProcessPayOsWebhook_WhenPaymentSuccess_PublishesPaymentSucceededV1ToHarness()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness();

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var publishEndpoint = provider.GetRequiredService<IPublishEndpoint>();
            var publisher = new OutboxEventPublisher(publishEndpoint, NullLogger<OutboxEventPublisher>.Instance);

            var orderId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var siteId = Guid.NewGuid();
            long orderCode = 123456;

            var gatewayPort = new FakePaymentGatewayPort
            {
                CallbackResult = new PaymentCallbackResult(
                    IsValidSignature: true,
                    OrderCode: orderCode,
                    AmountVnd: 25000,
                    GatewayTransactionId: "PAYOS-TXN-101",
                    BankReference: "REF-101",
                    PaymentStatus: "SUCCESS",
                    TransactionTime: DateTimeOffset.UtcNow,
                    RawPayload: "{\"orderCode\":123456}"
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
                FinalAmount = 25000,
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
                InvoiceNumber = "INV-20261102-0002",
                ReferenceType = ReferenceType.PARKING_SESSION,
                ReferenceId = sessionId,
                BaseParkingFee = 25000,
                OverstayPenaltyFee = 0,
                FinalAmount = 25000,
                PricingSnapshot = new InvoicePricingSnapshot
                {
                    Status = "PENDING",
                    PlateNumber = "29-B1 123.45",
                    SubtotalParkingFee = 25000,
                    TotalPenaltyFee = 0,
                    FinalTotalAmount = 25000
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

            var result = await handler.Handle(
                new ProcessPayOsWebhookCommand("{\"orderCode\":123456}", "valid_sig"),
                CancellationToken.None
            );

            Assert.NotNull(result);
            Assert.True(result.Success);

            Assert.True(await harness.Published.Any<PaymentSucceededV1>());
            var published = harness.Published.Select<PaymentSucceededV1>().FirstOrDefault()?.Context.Message;
            Assert.NotNull(published);
            Assert.Equal(orderId, published!.PaymentOrderId);
            Assert.Equal("PAYOS", published.PaymentMethod);
            Assert.Equal(25000m, published.Amount);
            Assert.Equal("29-B1 123.45", published.PlateNumber);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
