using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Usecase.GetInvoice;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class InvoiceQueryHandlerTests
{
    private readonly FakeInvoiceRepository _invoiceRepo = new();
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakeCurrentActor _actor = new();

    private (Invoice invoice, PaymentOrder order) SeedInvoiceAndOrder(
        bool isPaid = true,
        Guid? accountId = null,
        Guid? tenantId = null,
        Guid? siteId = null,
        Guid? sessionId = null)
    {
        var tenant = tenantId ?? Guid.NewGuid();
        var site = siteId ?? Guid.NewGuid();
        var session = sessionId ?? Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = tenant,
            SiteId = site,
            AccountId = accountId,
            OrderCode = "20261102001",
            Status = isPaid ? PaymentOrderStatus.PAID : PaymentOrderStatus.PENDING,
            FinalAmount = 65000,
            ReferenceId = session,
            ReferenceType = ReferenceType.PARKING_SESSION
        };

        var invoice = new Invoice
        {
            Id = invoiceId,
            TenantId = tenant,
            SiteId = site,
            PaymentOrderId = orderId,
            SessionId = session,
            ReferenceId = session,
            InvoiceNumber = "INV-20261102-0001",
            FinalAmount = 65000,
            BaseParkingFee = 45000,
            OverstayPenaltyFee = 20000,
            TotalParkedMinutes = 210,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = "20261102001",
                AccountId = accountId,
                CustomerType = accountId.HasValue ? "REGISTERED_DRIVER" : "GUEST_DRIVER",
                SiteName = "Landmark 81 Parking",
                VehicleType = "CAR",
                PlateNumber = "51A-123.45",
                EntryTime = DateTimeOffset.UtcNow.AddMinutes(-210),
                PricingCutoffAt = DateTimeOffset.UtcNow,
                TotalDurationMinutes = 210,
                BillableDurationMinutes = 210,
                TariffId = Guid.NewGuid(),
                TariffVersion = 2,
                PolicyName = "Standard Car",
                FreeParkingGraceMinutes = 15,
                SubtotalParkingFee = 45000,
                TotalPenaltyFee = 20000,
                FinalTotalAmount = 65000,
                Currency = "VND",
                Status = isPaid ? "PAID" : "PENDING",
                IsSettled = isPaid,
                PaidAt = isPaid ? DateTimeOffset.UtcNow : null,
                PaymentMethod = isPaid ? "PAYOS" : null,
                LineItems = new List<InvoiceLineItem>
                {
                    new()
                    {
                        ItemType = "PARKING_TIME_BLOCK",
                        Description = "Time block morning",
                        Amount = 45000,
                        DurationMinutes = 210
                    }
                }
            }
        };

        _orderRepo.Orders.Add(order);
        _invoiceRepo.Invoices.Add(invoice);

        return (invoice, order);
    }

    [Fact]
    public async Task GetInvoiceById_HappyPath_ReturnsCustomerInvoiceResponseDto()
    {
        var driverId = Guid.NewGuid();
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true, accountId: driverId);

        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var result = await handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(invoice.Id, result.InvoiceIdentity.InvoiceId);
        Assert.Equal(invoice.InvoiceNumber, result.InvoiceIdentity.InvoiceNumber);
        Assert.Equal("PAID", result.FinancialStatus.Status);
        Assert.True(result.FinancialStatus.IsSettled);
        Assert.Equal(65000, result.FinancialSummary.FinalTotalAmount);
    }

    [Fact]
    public async Task GetInvoiceById_UnsettledPendingOrder_ThrowsUnsettledPaymentOrderException()
    {
        var driverId = Guid.NewGuid();
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: false, accountId: driverId);

        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None));

        Assert.Equal(PaymentDomainException.UnsettledPaymentOrder, ex.Code);
    }

    [Fact]
    public async Task GetInvoiceById_DifferentDriverAccess_ThrowsForbiddenInvoiceAccessException()
    {
        var driverOwnerId = Guid.NewGuid();
        var driverAttackerId = Guid.NewGuid();
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true, accountId: driverOwnerId);

        _actor.UserId = driverAttackerId; // Different Driver!
        _actor.Role = "DRIVER";

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None));

        Assert.Equal(PaymentDomainException.ForbiddenInvoiceAccess, ex.Code);
    }

    [Fact]
    public async Task GetInvoiceById_OperatorSiteMismatch_ThrowsOperatorSiteMismatchException()
    {
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true, siteId: siteA);

        _actor.Role = "OPERATOR";
        _actor.SiteId = siteB; // Operator stationed at site B!

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None));

        Assert.Equal(PaymentDomainException.OperatorSiteMismatch, ex.Code);
    }

    [Fact]
    public async Task GetInvoiceById_CrossTenantAdmin_ThrowsCrossTenantAccessDeniedException()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true, tenantId: tenantA);

        _actor.Role = "ADMIN";
        _actor.TenantId = tenantB; // Admin belonging to tenant B!

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None));

        Assert.Equal(PaymentDomainException.CrossTenantAccessDenied, ex.Code);
    }

    [Fact]
    public async Task GetInvoiceById_OperatorWithNullSiteId_ThrowsOperatorSiteMismatch()
    {
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true);

        _actor.Role = "OPERATOR";
        _actor.SiteId = null;

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None));

        Assert.Equal(PaymentDomainException.OperatorSiteMismatch, ex.Code);
    }

    [Fact]
    public async Task GetInvoiceById_AdminWithNullTenantId_ThrowsCrossTenantAccessDenied()
    {
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true);

        _actor.Role = "ADMIN";
        _actor.TenantId = null;

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None));

        Assert.Equal(PaymentDomainException.CrossTenantAccessDenied, ex.Code);
    }

    [Fact]
    public async Task GetInvoiceById_UnrecognizedRole_ThrowsForbiddenInvoiceAccess()
    {
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true);

        _actor.Role = "AUDITOR"; // Unrecognized role

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None));

        Assert.Equal(PaymentDomainException.ForbiddenInvoiceAccess, ex.Code);
    }

    [Fact]
    public async Task GetInvoiceById_InternalService_HasFullAccess()
    {
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true);

        _actor.IsInternalService = true;

        var handler = new GetInvoiceByIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceByIdQueryHandler>.Instance);
        var result = await handler.Handle(new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(invoice.Id, result.InvoiceIdentity.InvoiceId);
    }

    [Fact]
    public async Task GetInvoiceByOrderId_HappyPath_ReturnsInvoice()
    {
        var driverId = Guid.NewGuid();
        var (invoice, order) = SeedInvoiceAndOrder(isPaid: true, accountId: driverId);

        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var handler = new GetInvoiceByOrderIdQueryHandler(_orderRepo, _invoiceRepo, _actor, NullLogger<GetInvoiceByOrderIdQueryHandler>.Instance);
        var result = await handler.Handle(new GetInvoiceByOrderIdQuery(order.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(invoice.InvoiceNumber, result.InvoiceIdentity.InvoiceNumber);
    }

    [Fact]
    public async Task GetInvoiceByOrderId_PendingOrder_ThrowsUnsettledPaymentOrderException()
    {
        var (_, order) = SeedInvoiceAndOrder(isPaid: false);

        _actor.IsInternalService = true;

        var handler = new GetInvoiceByOrderIdQueryHandler(_orderRepo, _invoiceRepo, _actor, NullLogger<GetInvoiceByOrderIdQueryHandler>.Instance);
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(new GetInvoiceByOrderIdQuery(order.Id), CancellationToken.None));

        Assert.Equal(PaymentDomainException.UnsettledPaymentOrder, ex.Code);
    }

    [Fact]
    public async Task GetInvoiceBySessionId_HappyPath_ReturnsInvoice()
    {
        var sessionId = Guid.NewGuid();
        var (invoice, _) = SeedInvoiceAndOrder(isPaid: true, sessionId: sessionId);

        _actor.IsInternalService = true;

        var handler = new GetInvoiceBySessionIdQueryHandler(_invoiceRepo, _orderRepo, _actor, NullLogger<GetInvoiceBySessionIdQueryHandler>.Instance);
        var result = await handler.Handle(new GetInvoiceBySessionIdQuery(sessionId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(invoice.InvoiceNumber, result.InvoiceIdentity.InvoiceNumber);
        Assert.Equal(sessionId, result.ParkingSessionReference.SessionId);
    }
}
