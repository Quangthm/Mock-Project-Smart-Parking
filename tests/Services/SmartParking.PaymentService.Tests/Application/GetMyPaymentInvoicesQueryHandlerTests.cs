using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Application.Usecase.GetMyInvoices;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class GetMyPaymentInvoicesQueryHandlerTests
{
    private readonly FakeInvoiceRepository _invoiceRepo = new();

    private GetMyPaymentInvoicesQueryHandler CreateHandler(FakeCurrentActor actor)
    {
        return new GetMyPaymentInvoicesQueryHandler(
            _invoiceRepo,
            actor,
            NullLogger<GetMyPaymentInvoicesQueryHandler>.Instance
        );
    }

    private static Invoice CreateInvoice(
        Guid id,
        Guid orderId,
        Guid accountId,
        DateTimeOffset createdAt,
        string orderCode = "ORD-001",
        PaymentOrderStatus orderStatus = PaymentOrderStatus.PAID)
    {
        var statusStr = orderStatus == PaymentOrderStatus.PAID ? "PAID" : "PENDING";
        var isSettled = orderStatus == PaymentOrderStatus.PAID;

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = accountId,
            OrderCode = orderCode,
            Status = orderStatus,
            Amount = 30000m,
            FinalAmount = 30000m,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        return new Invoice
        {
            Id = id,
            TenantId = order.TenantId,
            SiteId = order.SiteId,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            InvoiceNumber = $"INV-{createdAt:yyyyMMdd}-{id.ToString()[..4]}",
            BaseParkingFee = 30000m,
            OverstayPenaltyFee = 0m,
            FinalAmount = 30000m,
            CreatedAt = createdAt,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = orderCode,
                AccountId = accountId,
                CustomerType = "REGISTERED_DRIVER",
                PlateNumber = "51A-123.45",
                Currency = "VND",
                Status = statusStr,
                IsSettled = isSettled,
                SubtotalParkingFee = 30000m,
                FinalTotalAmount = 30000m,
                EntryTime = createdAt.AddHours(-2),
                PricingCutoffAt = createdAt
            }
        };
    }

    [Fact]
    public async Task Handle_AuthenticatedDriverWithInvoices_ReturnsDriverInvoicesOnly()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor
        {
            IsAuthenticated = true,
            UserId = driverId,
            Role = "DRIVER"
        };

        var inv1 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, DateTimeOffset.UtcNow.AddDays(-1), "ORD-D1");
        var inv2 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, DateTimeOffset.UtcNow, "ORD-D2");
        var otherDriverInv = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "ORD-OTHER");

        _invoiceRepo.Invoices.AddRange(new[] { inv1, inv2, otherDriverInv });

        var handler = CreateHandler(actor);

        // Act
        var result = await handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        Assert.All(result.Items, r => Assert.Equal(driverId, r.CustomerReference.AccountId));
    }

    [Fact]
    public async Task Handle_AuthenticatedCustomerRole_ReturnsCustomerInvoices()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var actor = new FakeCurrentActor
        {
            IsAuthenticated = true,
            UserId = customerId,
            Role = "CUSTOMER"
        };

        var inv = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), customerId, DateTimeOffset.UtcNow, "ORD-CUST");
        _invoiceRepo.Invoices.Add(inv);

        var handler = CreateHandler(actor);

        // Act
        var result = await handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(customerId, result.Items[0].CustomerReference.AccountId);
    }

    [Fact]
    public async Task Handle_DriverWithNoInvoices_ReturnsEmptyList()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor
        {
            IsAuthenticated = true,
            UserId = driverId,
            Role = "DRIVER"
        };

        var handler = CreateHandler(actor);

        // Act
        var result = await handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Handle_StrictAccountIsolation_DoesNotLeakOtherDriversInvoices()
    {
        // Arrange
        var driverA = Guid.NewGuid();
        var driverB = Guid.NewGuid();

        var invA = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverA, DateTimeOffset.UtcNow, "ORD-A");
        var invB = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverB, DateTimeOffset.UtcNow, "ORD-B");

        _invoiceRepo.Invoices.AddRange(new[] { invA, invB });

        var actorA = new FakeCurrentActor { IsAuthenticated = true, UserId = driverA, Role = "DRIVER" };
        var handlerA = CreateHandler(actorA);

        // Act
        var resultA = await handlerA.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None);

        // Assert
        Assert.Single(resultA.Items);
        Assert.Equal(driverA, resultA.Items[0].CustomerReference.AccountId);
        Assert.DoesNotContain(resultA.Items, r => r.CustomerReference.AccountId == driverB);
    }

    [Fact]
    public async Task Handle_UnauthenticatedActor_ThrowsUnauthorizedException()
    {
        // Arrange
        var actor = new FakeCurrentActor
        {
            IsAuthenticated = false,
            UserId = Guid.NewGuid(),
            Role = "DRIVER"
        };

        var handler = CreateHandler(actor);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None));
        Assert.Equal(PaymentDomainException.Unauthorized, ex.ErrorCode);
    }

    [Fact]
    public async Task Handle_ActorWithNullUserId_ThrowsUnauthorizedException()
    {
        // Arrange
        var actor = new FakeCurrentActor
        {
            IsAuthenticated = true,
            UserId = null,
            Role = "DRIVER"
        };

        var handler = CreateHandler(actor);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None));
        Assert.Equal(PaymentDomainException.Unauthorized, ex.ErrorCode);
    }

    [Fact]
    public async Task Handle_ActorWithEmptyUserId_ThrowsUnauthorizedException()
    {
        // Arrange
        var actor = new FakeCurrentActor
        {
            IsAuthenticated = true,
            UserId = Guid.Empty,
            Role = "DRIVER"
        };

        var handler = CreateHandler(actor);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None));
        Assert.Equal(PaymentDomainException.Unauthorized, ex.ErrorCode);
    }

    [Theory]
    [InlineData("OPERATOR")]
    [InlineData("ADMIN")]
    [InlineData("AUDITOR")]
    public async Task Handle_DisallowedRole_ThrowsForbiddenInvoiceAccessException(string disallowedRole)
    {
        // Arrange
        var actor = new FakeCurrentActor
        {
            IsAuthenticated = true,
            UserId = Guid.NewGuid(),
            Role = disallowedRole
        };

        var handler = CreateHandler(actor);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None));
        Assert.Equal(PaymentDomainException.ForbiddenInvoiceAccess, ex.ErrorCode);
    }

    [Fact]
    public async Task Handle_InvoicesAreSortedDescendingByCreatedAt()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor { IsAuthenticated = true, UserId = driverId, Role = "DRIVER" };

        var t1 = DateTimeOffset.Parse("2026-11-01T10:00:00+07:00");
        var t2 = DateTimeOffset.Parse("2026-11-02T10:00:00+07:00");
        var t3 = DateTimeOffset.Parse("2026-11-03T10:00:00+07:00");

        var inv1 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, t1, "ORD-OLD");
        var inv2 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, t3, "ORD-NEWEST");
        var inv3 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, t2, "ORD-MIDDLE");

        // Insert out of chronological order
        _invoiceRepo.Invoices.AddRange(new[] { inv1, inv2, inv3 });

        var handler = CreateHandler(actor);

        // Act
        var result = await handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("ORD-NEWEST", result.Items[0].InvoiceIdentity.OrderCode);
        Assert.Equal("ORD-MIDDLE", result.Items[1].InvoiceIdentity.OrderCode);
        Assert.Equal("ORD-OLD", result.Items[2].InvoiceIdentity.OrderCode);
    }

    [Fact]
    public async Task Handle_StatusFilter_ReturnsOnlyMatchingStatusInvoices()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor { IsAuthenticated = true, UserId = driverId, Role = "DRIVER" };

        var paid1 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, DateTimeOffset.UtcNow.AddHours(-3), "ORD-P1", PaymentOrderStatus.PAID);
        var paid2 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, DateTimeOffset.UtcNow.AddHours(-2), "ORD-P2", PaymentOrderStatus.PAID);
        var pending = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, DateTimeOffset.UtcNow.AddHours(-1), "ORD-PEND", PaymentOrderStatus.PENDING);

        _invoiceRepo.Invoices.AddRange(new[] { paid1, paid2, pending });

        var handler = CreateHandler(actor);

        // Act: Filter PAID
        var resultPaid = await handler.Handle(new GetMyPaymentInvoicesQuery(Status: "PAID"), CancellationToken.None);

        // Assert PAID
        Assert.Equal(2, resultPaid.TotalCount);
        Assert.Equal(2, resultPaid.Items.Count);
        Assert.All(resultPaid.Items, r => Assert.Equal("PAID", r.FinancialStatus.Status));

        // Act: Filter PENDING
        var resultPending = await handler.Handle(new GetMyPaymentInvoicesQuery(Status: "PENDING"), CancellationToken.None);

        // Assert PENDING
        Assert.Equal(1, resultPending.TotalCount);
        Assert.Single(resultPending.Items);
        Assert.Equal("PENDING", resultPending.Items[0].FinancialStatus.Status);
    }

    [Fact]
    public async Task Handle_DateRangeFilter_ReturnsInvoicesWithinRange()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor { IsAuthenticated = true, UserId = driverId, Role = "DRIVER" };

        var t1 = DateTimeOffset.Parse("2026-11-01T10:00:00+00:00");
        var t2 = DateTimeOffset.Parse("2026-11-05T10:00:00+00:00");
        var t3 = DateTimeOffset.Parse("2026-11-10T10:00:00+00:00");

        var inv1 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, t1, "ORD-NOV01");
        var inv2 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, t2, "ORD-NOV05");
        var inv3 = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, t3, "ORD-NOV10");

        _invoiceRepo.Invoices.AddRange(new[] { inv1, inv2, inv3 });

        var handler = CreateHandler(actor);

        // Act: Query between Nov 03 and Nov 07
        var fromDate = DateTimeOffset.Parse("2026-11-03T00:00:00+00:00");
        var toDate = DateTimeOffset.Parse("2026-11-07T23:59:59+00:00");
        var query = new GetMyPaymentInvoicesQuery(FromDate: fromDate, ToDate: toDate);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("ORD-NOV05", result.Items[0].InvoiceIdentity.OrderCode);
    }

    [Fact]
    public async Task Handle_Pagination_ReturnsCorrectSliceAndMetadata()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor { IsAuthenticated = true, UserId = driverId, Role = "DRIVER" };

        var baseTime = DateTimeOffset.UtcNow;
        for (int i = 1; i <= 5; i++)
        {
            _invoiceRepo.Invoices.Add(CreateInvoice(
                Guid.NewGuid(),
                Guid.NewGuid(),
                driverId,
                baseTime.AddHours(-i),
                $"ORD-{i:D2}"));
        }

        var handler = CreateHandler(actor);

        // Act: Page 2 with PageSize 2
        var query = new GetMyPaymentInvoicesQuery(PageNumber: 2, PageSize: 2);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("ORD-03", result.Items[0].InvoiceIdentity.OrderCode);
        Assert.Equal("ORD-04", result.Items[1].InvoiceIdentity.OrderCode);
    }

    [Fact]
    public async Task Handle_PageNumberExceedsTotalPages_ReturnsEmptyItems()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor { IsAuthenticated = true, UserId = driverId, Role = "DRIVER" };

        for (int i = 1; i <= 3; i++)
        {
            _invoiceRepo.Invoices.Add(CreateInvoice(
                Guid.NewGuid(),
                Guid.NewGuid(),
                driverId,
                DateTimeOffset.UtcNow.AddHours(-i),
                $"ORD-{i}"));
        }

        var handler = CreateHandler(actor);

        // Act: Page 5 with PageSize 2
        var query = new GetMyPaymentInvoicesQuery(PageNumber: 5, PageSize: 2);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(5, result.PageNumber);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_ReturnedInvoicesIncludePdfUrl()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor { IsAuthenticated = true, UserId = driverId, Role = "DRIVER" };

        var inv = CreateInvoice(Guid.NewGuid(), Guid.NewGuid(), driverId, DateTimeOffset.UtcNow, "ORD-PDF-CHECK");
        _invoiceRepo.Invoices.Add(inv);

        var handler = CreateHandler(actor);

        // Act
        var result = await handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.NotNull(item.PdfUrl);
        Assert.Equal($"/api/v1/payments/invoices/{inv.Id}/download", item.PdfUrl);
        Assert.Equal($"/api/v1/payments/invoices/{inv.Id}/download", item.InvoiceIdentity.PdfUrl);
    }
}
