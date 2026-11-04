using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging.Abstractions;

using SmartParking.PaymentService.Domain.Entities;

using SmartParking.PaymentService.Domain.Enums;

using SmartParking.PaymentService.Domain.Events;

using SmartParking.PaymentService.Persistence;

using SmartParking.PaymentService.Persistence.Outbox;

using SmartParking.PaymentService.Persistence.Repositories;
using SmartParking.PaymentService.Tests.TestDoubles;

using Xunit;



namespace SmartParking.PaymentService.Tests.Persistence;



public class InvoiceRepositoryTests

{

    private static PaymentDbContext CreateInMemoryDbContext()

    {

        var options = new DbContextOptionsBuilder<PaymentDbContext>()

            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())

            .Options;



        return new PaymentDbContext(options);

    }



    [Fact]

    public async Task InvoiceRepository_CrudAndLookups_WorkCorrectly()

    {

        using var dbContext = CreateInMemoryDbContext();

        var repository = new InvoiceRepository(dbContext);



        var tenantId = Guid.NewGuid();

        var siteId = Guid.NewGuid();

        var orderId = Guid.NewGuid();

        var sessionId = Guid.NewGuid();

        var invoiceId = Guid.NewGuid();



        var order = new PaymentOrder

        {

            Id = orderId,

            TenantId = tenantId,

            SiteId = siteId,

            OrderCode = "ORD-TEST-001",

            Status = PaymentOrderStatus.PAID,

            FinalAmount = 50000,

            ReferenceId = sessionId,

            ReferenceType = ReferenceType.PARKING_SESSION

        };

        dbContext.PaymentOrders.Add(order);

        await dbContext.SaveChangesAsync();



        var invoice = new Invoice

        {

            Id = invoiceId,

            TenantId = tenantId,

            SiteId = siteId,

            PaymentOrderId = orderId,

            SessionId = sessionId,

            ReferenceId = sessionId,

            InvoiceNumber = "INV-20261102-0001",

            TotalParkedMinutes = 120,

            BaseParkingFee = 50000,

            OverstayPenaltyFee = 0,

            FinalAmount = 50000,

            PricingSnapshot = new InvoicePricingSnapshot

            {

                OrderCode = "ORD-TEST-001",

                PlateNumber = "51A-999.99",

                Status = "PAID",

                IsSettled = true

            }

        };



        await repository.AddAsync(invoice);

        await dbContext.SaveChangesAsync();



        // 1. GetByIdAsync

        var byId = await repository.GetByIdAsync(invoiceId);

        Assert.NotNull(byId);

        Assert.Equal("INV-20261102-0001", byId.InvoiceNumber);



        // 2. GetByPaymentOrderIdAsync

        var byOrder = await repository.GetByPaymentOrderIdAsync(orderId);

        Assert.NotNull(byOrder);

        Assert.Equal(invoiceId, byOrder.Id);



        // 3. GetBySessionIdAsync

        var bySession = await repository.GetBySessionIdAsync(sessionId);

        Assert.NotNull(bySession);

        Assert.Equal(invoiceId, bySession.Id);



        // 4. GetByInvoiceNumberAsync

        var byNumber = await repository.GetByInvoiceNumberAsync("INV-20261102-0001");

        Assert.NotNull(byNumber);

        Assert.Equal(invoiceId, byNumber.Id);

    }



    [Fact]

    public async Task GenerateInvoiceNumberAsync_ProducesIncrementingStandardFormat()

    {

        using var dbContext = CreateInMemoryDbContext();

        var repository = new InvoiceRepository(dbContext);



        var num1 = await repository.GenerateInvoiceNumberAsync();

        Assert.Matches(@"^INV-\d{8}-0001$", num1);



        // Add dummy invoice with num1 to simulate existing record

        dbContext.Invoices.Add(new Invoice

        {

            Id = Guid.NewGuid(),

            TenantId = Guid.NewGuid(),

            SiteId = Guid.NewGuid(),

            PaymentOrderId = Guid.NewGuid(),

            ReferenceId = Guid.NewGuid(),

            InvoiceNumber = num1,

            FinalAmount = 10000

        });

        await dbContext.SaveChangesAsync();



        var num2 = await repository.GenerateInvoiceNumberAsync();

        Assert.Matches(@"^INV-\d{8}-0002$", num2);

    }



    [Fact]

    public async Task OutboxEventPublisher_PublishesIntegrationEvent()

    {

        var fakePublishEndpoint = new FakePublishEndpoint();

        var publisher = new OutboxEventPublisher(fakePublishEndpoint, NullLogger<OutboxEventPublisher>.Instance);



        var @event = new PaymentSucceededV1

        {

            EventId = Guid.NewGuid(),

            OccurredAt = DateTimeOffset.UtcNow,

            CorrelationId = "CORR-01",

            PaymentOrderId = Guid.NewGuid(),

            PaymentTransactionId = Guid.NewGuid(),

            PaymentMethod = "CASH",

            TenantId = Guid.NewGuid(),

            SiteId = Guid.NewGuid(),

            ReferenceId = Guid.NewGuid(),

            Amount = 45000,

            ParkingFee = 45000,

            Currency = "VND"

        };



        await publisher.PublishAsync(@event);



        Assert.Single(fakePublishEndpoint.PublishedMessages);

        var published = Assert.IsType<PaymentSucceededV1>(fakePublishEndpoint.PublishedMessages[0]);

        Assert.Equal("CORR-01", published.CorrelationId);

    }



    [Fact]

    public async Task CollectCashPayment_WithRealDbContext_AtomicallyPersistsOrderInvoiceAndOutboxMessage()

    {

        using var dbContext = CreateInMemoryDbContext();

        var orderRepo = new PaymentOrderRepository(dbContext);

        var txRepo = new PaymentTransactionRepository(dbContext);

        var invoiceRepo = new InvoiceRepository(dbContext);

        var uow = new UnitOfWork(dbContext);

        var fakePublishEndpoint = new FakePublishEndpoint();

        var publisher = new OutboxEventPublisher(fakePublishEndpoint, NullLogger<OutboxEventPublisher>.Instance);



        var tenantId = Guid.NewGuid();

        var siteId = Guid.NewGuid();

        var orderId = Guid.NewGuid();

        var sessionId = Guid.NewGuid();



        var order = new PaymentOrder

        {

            Id = orderId,

            TenantId = tenantId,

            SiteId = siteId,

            OrderCode = "ORD-ATOMIC-001",

            Status = PaymentOrderStatus.PENDING,

            FinalAmount = 65000,

            ReferenceId = sessionId,

            ReferenceType = ReferenceType.PARKING_SESSION

        };

        dbContext.PaymentOrders.Add(order);



        var invoice = new Invoice

        {

            Id = Guid.NewGuid(),

            TenantId = tenantId,

            SiteId = siteId,

            PaymentOrderId = orderId,

            SessionId = sessionId,

            ReferenceId = sessionId,

            InvoiceNumber = "INV-20261102-0099",

            FinalAmount = 65000,

            BaseParkingFee = 45000,

            OverstayPenaltyFee = 20000,

            PricingSnapshot = new InvoicePricingSnapshot

            {

                OrderCode = "ORD-ATOMIC-001",

                Status = "PENDING",

                IsSettled = false,

                PlateNumber = "51A-777.77"

            }

        };

        dbContext.Invoices.Add(invoice);

        await dbContext.SaveChangesAsync();



        var actor = new SmartParking.PaymentService.Tests.TestDoubles.FakeCurrentActor

        {

            Role = "OPERATOR",

            SiteId = siteId,

            TenantId = tenantId

        };



        var handler = new SmartParking.PaymentService.Application.Usecase.CollectCashPayment.CollectCashPaymentCommandHandler(

            orderRepo,

            txRepo,

            invoiceRepo,

            uow,

            publisher,

            actor,

            NullLogger<SmartParking.PaymentService.Application.Usecase.CollectCashPayment.CollectCashPaymentCommandHandler>.Instance

        );



        var result = await handler.Handle(

            new SmartParking.PaymentService.Application.Usecase.CollectCashPayment.CollectCashPaymentCommand(orderId),

            CancellationToken.None

        );



        Assert.NotNull(result);

        Assert.Equal("PAID", result.FinancialStatus.Status);



        // Verify that the outbox integration event was enqueued

        Assert.Single(fakePublishEndpoint.PublishedMessages);

        var savedOutbox = Assert.IsType<PaymentSucceededV1>(fakePublishEndpoint.PublishedMessages[0]);

        Assert.Equal(orderId, savedOutbox.PaymentOrderId);

        Assert.Equal("51A-777.77", savedOutbox.PlateNumber);



        // Verify order is PAID in database

        var savedOrder = await dbContext.PaymentOrders.FirstOrDefaultAsync(o => o.Id == orderId);

        Assert.NotNull(savedOrder);

        Assert.Equal(PaymentOrderStatus.PAID, savedOrder.Status);



        // Verify invoice is PAID in database
        var savedInvoice = await dbContext.Invoices.FirstOrDefaultAsync(i => i.PaymentOrderId == orderId);
        Assert.NotNull(savedInvoice);
        Assert.Equal("PAID", savedInvoice.PricingSnapshot.Status);
    }

    [Fact]
    public async Task GetByAccountIdAsync_FiltersByPaymentOrderAccountId_AndSortsDescending()
    {
        using var dbContext = CreateInMemoryDbContext();
        var repository = new InvoiceRepository(dbContext);

        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var targetAccountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();

        var order1 = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            AccountId = targetAccountId,
            OrderCode = "ORD-01",
            Status = PaymentOrderStatus.PAID,
            FinalAmount = 25000,
            ReferenceId = Guid.NewGuid(),
            ReferenceType = ReferenceType.PARKING_SESSION
        };

        var order2 = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            AccountId = targetAccountId,
            OrderCode = "ORD-02",
            Status = PaymentOrderStatus.PAID,
            FinalAmount = 50000,
            ReferenceId = Guid.NewGuid(),
            ReferenceType = ReferenceType.PARKING_SESSION
        };

        var otherOrder = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            AccountId = otherAccountId,
            OrderCode = "ORD-OTHER",
            Status = PaymentOrderStatus.PAID,
            FinalAmount = 10000,
            ReferenceId = Guid.NewGuid(),
            ReferenceType = ReferenceType.PARKING_SESSION
        };

        dbContext.PaymentOrders.AddRange(order1, order2, otherOrder);

        var inv1 = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            PaymentOrderId = order1.Id,
            PaymentOrder = order1,
            ReferenceId = order1.ReferenceId,
            InvoiceNumber = "INV-001",
            FinalAmount = 25000,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };

        var inv2 = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            PaymentOrderId = order2.Id,
            PaymentOrder = order2,
            ReferenceId = order2.ReferenceId,
            InvoiceNumber = "INV-002",
            FinalAmount = 50000,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var invOther = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            PaymentOrderId = otherOrder.Id,
            PaymentOrder = otherOrder,
            ReferenceId = otherOrder.ReferenceId,
            InvoiceNumber = "INV-OTHER",
            FinalAmount = 10000,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Invoices.AddRange(inv1, inv2, invOther);
        await dbContext.SaveChangesAsync();

        // Act
        var results = await repository.GetByAccountIdAsync(targetAccountId);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.Equal("INV-002", results[0].InvoiceNumber); // Newest first
        Assert.Equal("INV-001", results[1].InvoiceNumber);
        Assert.All(results, inv => Assert.Equal(targetAccountId, inv.PaymentOrder!.AccountId));
    }
}

