using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Usecase.GetInvoicePdf;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class DownloadInvoicePdfQueryHandlerTests
{
    private readonly FakeInvoiceRepository _invoiceRepo = new();
    private readonly FakePdfInvoiceGenerator _pdfGenerator = new();
    private readonly FakeCurrentActor _actor = new();

    private DownloadInvoicePdfQueryHandler CreateHandler()
    {
        return new DownloadInvoicePdfQueryHandler(
            _invoiceRepo,
            _pdfGenerator,
            _actor,
            NullLogger<DownloadInvoicePdfQueryHandler>.Instance
        );
    }

    private Invoice SeedInvoice(
        bool isPaid = true,
        Guid? accountId = null,
        Guid? siteId = null,
        Guid? tenantId = null,
        string invoiceNumber = "INV-20261110-0001",
        PaymentOrderStatus? orderStatus = null)
    {
        var tenant = tenantId ?? Guid.NewGuid();
        var site = siteId ?? Guid.NewGuid();
        var acc = accountId ?? Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var status = orderStatus ?? (isPaid ? PaymentOrderStatus.PAID : PaymentOrderStatus.PENDING);

        var order = new PaymentOrder
        {
            Id = orderId,
            TenantId = tenant,
            SiteId = site,
            AccountId = acc,
            OrderCode = "ORD-TEST-001",
            Status = status,
            FinalAmount = 45000,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var invoice = new Invoice
        {
            Id = invoiceId,
            TenantId = tenant,
            SiteId = site,
            PaymentOrderId = orderId,
            PaymentOrder = order,
            InvoiceNumber = invoiceNumber,
            BaseParkingFee = 45000,
            FinalAmount = 45000,
            CreatedAt = DateTimeOffset.UtcNow,
            PricingSnapshot = new InvoicePricingSnapshot
            {
                OrderCode = "ORD-TEST-001",
                AccountId = acc,
                SiteName = "Landmark 81 Central",
                PlateNumber = "51A-123.45",
                VehicleType = "CAR",
                Currency = "VND",
                Status = isPaid ? "PAID" : "PENDING",
                IsSettled = isPaid,
                SubtotalParkingFee = 45000,
                FinalTotalAmount = 45000,
                EntryTime = DateTimeOffset.UtcNow.AddHours(-2),
                PricingCutoffAt = DateTimeOffset.UtcNow
            }
        };

        _invoiceRepo.Invoices.Add(invoice);
        return invoice;
    }

    [Fact]
    public async Task Handle_SettledInvoiceForOwnerDriver_ReturnsValidPdfBytesAndFileName()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: true, accountId: driverId, invoiceNumber: "INV-20261110-0001");

        _actor.IsAuthenticated = true;
        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("HD_INV-20261110-0001.pdf", result.FileName);
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.Content);
        Assert.Equal(1, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_SettledInvoiceForCustomerRole_ReturnsValidPdfBytes()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: true, accountId: customerId, invoiceNumber: "INV-20261110-0002");

        _actor.IsAuthenticated = true;
        _actor.UserId = customerId;
        _actor.Role = "CUSTOMER";

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("HD_INV-20261110-0002.pdf", result.FileName);
        Assert.Equal(1, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_SettledInvoiceForOperatorWithMatchingSite_ReturnsValidPdfBytes()
    {
        // Arrange
        var siteId = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: true, siteId: siteId, invoiceNumber: "INV-20261110-0003");

        _actor.IsAuthenticated = true;
        _actor.UserId = Guid.NewGuid();
        _actor.Role = "OPERATOR";
        _actor.SiteId = siteId;

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(1, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_SettledInvoiceForAdminWithMatchingTenant_ReturnsValidPdfBytes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: true, tenantId: tenantId, invoiceNumber: "INV-20261110-0004");

        _actor.IsAuthenticated = true;
        _actor.UserId = Guid.NewGuid();
        _actor.Role = "ADMIN";
        _actor.TenantId = tenantId;

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(1, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_SettledInvoiceForInternalService_ReturnsValidPdfBytes()
    {
        // Arrange
        var invoice = SeedInvoice(isPaid: true, invoiceNumber: "INV-20261110-0005");

        _actor.IsAuthenticated = false;
        _actor.IsInternalService = true;

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(1, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_UnsettledPendingInvoice_ThrowsUnsettledPaymentOrderException()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: false, accountId: driverId, orderStatus: PaymentOrderStatus.PENDING);

        _actor.IsAuthenticated = true;
        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var handler = CreateHandler();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None));
        Assert.Equal(PaymentDomainException.UnsettledPaymentOrder, ex.ErrorCode);
        Assert.Equal(0, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_UnsettledFailedInvoice_ThrowsUnsettledPaymentOrderException()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: false, accountId: driverId, orderStatus: PaymentOrderStatus.FAILED);

        _actor.IsAuthenticated = true;
        _actor.UserId = driverId;
        _actor.Role = "DRIVER";

        var handler = CreateHandler();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None));
        Assert.Equal(PaymentDomainException.UnsettledPaymentOrder, ex.ErrorCode);
        Assert.Equal(0, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_DifferentDriverInvoice_ThrowsForbiddenInvoiceAccessException()
    {
        // Arrange
        var ownerDriverId = Guid.NewGuid();
        var callingDriverId = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: true, accountId: ownerDriverId);

        _actor.IsAuthenticated = true;
        _actor.UserId = callingDriverId;
        _actor.Role = "DRIVER";

        var handler = CreateHandler();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None));
        Assert.Equal(PaymentDomainException.ForbiddenInvoiceAccess, ex.ErrorCode);
        Assert.Equal(0, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_OperatorSiteMismatch_ThrowsOperatorSiteMismatchException()
    {
        // Arrange
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: true, siteId: siteA);

        _actor.IsAuthenticated = true;
        _actor.UserId = Guid.NewGuid();
        _actor.Role = "OPERATOR";
        _actor.SiteId = siteB;

        var handler = CreateHandler();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None));
        Assert.Equal(PaymentDomainException.OperatorSiteMismatch, ex.ErrorCode);
        Assert.Equal(0, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_AdminTenantMismatch_ThrowsCrossTenantAccessDeniedException()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var invoice = SeedInvoice(isPaid: true, tenantId: tenantA);

        _actor.IsAuthenticated = true;
        _actor.UserId = Guid.NewGuid();
        _actor.Role = "ADMIN";
        _actor.TenantId = tenantB;

        var handler = CreateHandler();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None));
        Assert.Equal(PaymentDomainException.CrossTenantAccessDenied, ex.ErrorCode);
        Assert.Equal(0, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_NonExistentInvoice_ThrowsInvoiceNotFoundException()
    {
        // Arrange
        _actor.IsAuthenticated = true;
        _actor.UserId = Guid.NewGuid();
        _actor.Role = "DRIVER";

        var handler = CreateHandler();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new DownloadInvoicePdfQuery(Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(PaymentDomainException.InvoiceNotFound, ex.ErrorCode);
        Assert.Equal(0, _pdfGenerator.CallCount);
    }

    [Fact]
    public async Task Handle_UnauthenticatedActor_ThrowsUnauthorizedException()
    {
        // Arrange
        var invoice = SeedInvoice(isPaid: true);

        _actor.IsAuthenticated = false;
        _actor.IsInternalService = false;

        var handler = CreateHandler();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(
            () => handler.Handle(new DownloadInvoicePdfQuery(invoice.Id), CancellationToken.None));
        Assert.Equal(PaymentDomainException.Unauthorized, ex.ErrorCode);
        Assert.Equal(0, _pdfGenerator.CallCount);
    }
}
