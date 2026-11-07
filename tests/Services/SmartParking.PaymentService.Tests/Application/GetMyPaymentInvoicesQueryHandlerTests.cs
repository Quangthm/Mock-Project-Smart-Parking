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

    private static Invoice CreateInvoice(Guid id, Guid orderId, Guid accountId, DateTimeOffset createdAt, string orderCode = "ORD-001")
    {
        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = accountId,
            OrderCode = orderCode,
            Status = PaymentOrderStatus.PAID,
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
                Status = "PAID",
                IsSettled = true,
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
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(driverId, r.CustomerReference.AccountId));
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
        Assert.Single(result);
        Assert.Equal(customerId, result[0].CustomerReference.AccountId);
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
        Assert.Empty(result);
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
        Assert.Single(resultA);
        Assert.Equal(driverA, resultA[0].CustomerReference.AccountId);
        Assert.DoesNotContain(resultA, r => r.CustomerReference.AccountId == driverB);
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
        Assert.Equal(3, result.Count);
        Assert.Equal("ORD-NEWEST", result[0].InvoiceIdentity.OrderCode);
        Assert.Equal("ORD-MIDDLE", result[1].InvoiceIdentity.OrderCode);
        Assert.Equal("ORD-OLD", result[2].InvoiceIdentity.OrderCode);
    }
}
