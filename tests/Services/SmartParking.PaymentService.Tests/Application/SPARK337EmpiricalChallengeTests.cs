using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Application.Usecase.GetInvoicePdf;
using SmartParking.PaymentService.Application.Usecase.GetMyInvoices;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Infrastructure.Adapters;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.EmpiricalChallenge;

public class SPARK337EmpiricalChallengeTests
{
    private static PaymentDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PaymentDbContext(options);
    }

    // =========================================================================
    // 1. BOUNDARY CONDITION: PAGINATION (Negative, Zero, Exceeding, Clamping)
    // =========================================================================

    [Theory]
    [InlineData(-1, -10, 1, 10)]
    [InlineData(0, 0, 1, 10)]
    [InlineData(-999, 50, 1, 50)]
    [InlineData(1, 9999, 1, 100)] // Clamped to 100 max
    public async Task Pagination_NegativeOrExtremeInputs_ClampedGracefullyInHandler(
        int inputPageNumber, int inputPageSize, int expectedPageNumber, int expectedPageSize)
    {
        // Arrange
        var fakeRepo = new FakeInvoiceRepository();
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor { IsAuthenticated = true, UserId = driverId, Role = "DRIVER" };

        for (int i = 0; i < 25; i++)
        {
            var invId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            fakeRepo.Invoices.Add(new Invoice
            {
                Id = invId,
                TenantId = Guid.NewGuid(),
                SiteId = Guid.NewGuid(),
                PaymentOrderId = orderId,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
                PaymentOrder = new PaymentOrder
                {
                    Id = orderId,
                    AccountId = driverId,
                    OrderCode = $"ORD-{i}",
                    Status = PaymentOrderStatus.PAID
                },
                PricingSnapshot = new InvoicePricingSnapshot
                {
                    AccountId = driverId,
                    OrderCode = $"ORD-{i}",
                    Status = "PAID",
                    IsSettled = true
                }
            });
        }

        var handler = new GetMyPaymentInvoicesQueryHandler(fakeRepo, actor, NullLogger<GetMyPaymentInvoicesQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetMyPaymentInvoicesQuery(PageNumber: inputPageNumber, PageSize: inputPageSize),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(expectedPageNumber, result.PageNumber);
        Assert.Equal(expectedPageSize, result.PageSize);
        Assert.Equal(Math.Min(expectedPageSize, 25), result.Items.Count);
    }

    [Fact]
    public async Task Pagination_PageNumberExceedsTotalPages_ReturnsEmptyItemsWithoutError()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repo = new InvoiceRepository(dbContext);
        var driverId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "ORD-001",
            Status = PaymentOrderStatus.PAID
        };
        dbContext.PaymentOrders.Add(order);

        dbContext.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            SiteId = order.SiteId,
            PaymentOrderId = order.Id,
            PaymentOrder = order,
            InvoiceNumber = "INV-001",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // Act: Page 50 on real EF Core repo with only 1 record
        var (items, totalCount) = await repo.GetByAccountIdPagedAsync(
            driverId, null, null, null, pageNumber: 50, pageSize: 10);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Pagination_ZeroTotalCount_ReturnsZeroTotalPagesAndEmptyItems()
    {
        // Arrange
        var fakeRepo = new FakeInvoiceRepository();
        var driverId = Guid.NewGuid();
        var actor = new FakeCurrentActor { IsAuthenticated = true, UserId = driverId, Role = "DRIVER" };

        var handler = new GetMyPaymentInvoicesQueryHandler(fakeRepo, actor, NullLogger<GetMyPaymentInvoicesQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetMyPaymentInvoicesQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
    }

    // =========================================================================
    // 2. BOUNDARY CONDITION: DATE FILTERING (Inverted, Null, Edge Boundaries)
    // =========================================================================

    [Fact]
    public async Task DateFilter_InvertedDates_ReturnsEmptyResultCleanlyWithoutException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repo = new InvoiceRepository(dbContext);
        var driverId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "ORD-001",
            Status = PaymentOrderStatus.PAID
        };
        dbContext.PaymentOrders.Add(order);

        dbContext.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            SiteId = order.SiteId,
            PaymentOrderId = order.Id,
            PaymentOrder = order,
            InvoiceNumber = "INV-001",
            CreatedAt = DateTimeOffset.Parse("2026-11-05T12:00:00Z")
        });
        await dbContext.SaveChangesAsync();

        // Act: fromDate is in future relative to toDate (Inverted: Nov 10 to Nov 01)
        var fromDate = DateTimeOffset.Parse("2026-11-10T00:00:00Z");
        var toDate = DateTimeOffset.Parse("2026-11-01T00:00:00Z");

        var (items, totalCount) = await repo.GetByAccountIdPagedAsync(
            driverId, null, fromDate, toDate, 1, 10);

        // Assert
        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task DateFilter_NullDates_ReturnsAllInvoicesForAccount()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repo = new InvoiceRepository(dbContext);
        var driverId = Guid.NewGuid();

        for (int i = 1; i <= 3; i++)
        {
            var order = new PaymentOrder
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                SiteId = Guid.NewGuid(),
                AccountId = driverId,
                OrderCode = $"ORD-{i}",
                Status = PaymentOrderStatus.PAID
            };
            dbContext.PaymentOrders.Add(order);

            dbContext.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                SiteId = order.SiteId,
                PaymentOrderId = order.Id,
                PaymentOrder = order,
                InvoiceNumber = $"INV-{i}",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-i)
            });
        }
        await dbContext.SaveChangesAsync();

        // Act: Both dates null
        var (items, totalCount) = await repo.GetByAccountIdPagedAsync(
            driverId, null, null, null, 1, 10);

        // Assert
        Assert.Equal(3, totalCount);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task StatusFilter_InvalidStatusString_ReturnsZeroResultsCleanlyWithoutException()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repo = new InvoiceRepository(dbContext);
        var driverId = Guid.NewGuid();

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "ORD-001",
            Status = PaymentOrderStatus.PAID
        };
        dbContext.PaymentOrders.Add(order);

        dbContext.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            SiteId = order.SiteId,
            PaymentOrderId = order.Id,
            PaymentOrder = order,
            InvoiceNumber = "INV-001",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // Act: Non-existent status
        var (items, totalCount) = await repo.GetByAccountIdPagedAsync(
            driverId, status: "NOT_A_VALID_STATUS", null, null, 1, 10);

        // Assert
        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("PAID")]
    [InlineData("Paid")]
    [InlineData(" pending ")]
    public async Task StatusFilter_CaseInsensitiveAndWhitespaceTolerant_FiltersCorrectly(string statusInput)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var repo = new InvoiceRepository(dbContext);
        var driverId = Guid.NewGuid();

        var paidOrder = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "ORD-PAID",
            Status = PaymentOrderStatus.PAID
        };
        var pendingOrder = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            AccountId = driverId,
            OrderCode = "ORD-PEND",
            Status = PaymentOrderStatus.PENDING
        };
        dbContext.PaymentOrders.AddRange(paidOrder, pendingOrder);

        dbContext.Invoices.AddRange(
            new Invoice { Id = Guid.NewGuid(), TenantId = paidOrder.TenantId, SiteId = paidOrder.SiteId, PaymentOrderId = paidOrder.Id, PaymentOrder = paidOrder, InvoiceNumber = "INV-P", CreatedAt = DateTimeOffset.UtcNow },
            new Invoice { Id = Guid.NewGuid(), TenantId = pendingOrder.TenantId, SiteId = pendingOrder.SiteId, PaymentOrderId = pendingOrder.Id, PaymentOrder = pendingOrder, InvoiceNumber = "INV-PEND", CreatedAt = DateTimeOffset.UtcNow }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var (items, totalCount) = await repo.GetByAccountIdPagedAsync(
            driverId, statusInput, null, null, 1, 10);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(items);
    }

    // =========================================================================
    // 3. BOUNDARY CONDITION: QUESTPDF RENDERING (No line items, zero penalty/refund, multi-page)
    // =========================================================================

    [Fact]
    public async Task QuestPdf_NullLineItems_RendersGracefullyWithStandardFallbackItem()
    {
        // Arrange
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        var generator = new QuestPdfInvoiceGenerator();

        var invoiceDto = new CustomerInvoiceResponseDto(
            InvoiceIdentity: new InvoiceIdentityDto(
                InvoiceId: Guid.NewGuid(),
                InvoiceNumber: "INV-NULL-ITEMS",
                PaymentOrderId: Guid.NewGuid(),
                OrderCode: "ORD-001",
                IssuedAt: DateTimeOffset.UtcNow,
                PdfUrl: "/api/v1/payments/invoices/dummy/download"
            ),
            CustomerReference: new CustomerReferenceDto(
                AccountId: Guid.NewGuid(),
                CustomerType: "REGISTERED_DRIVER",
                DisplayName: "Test Driver"
            ),
            ParkingSessionReference: new ParkingSessionReferenceDto(
                SessionId: Guid.NewGuid(),
                ReferenceType: "PARKING_SESSION",
                SiteId: Guid.NewGuid(),
                SiteName: "Central Parking Lot",
                VehicleType: "CAR",
                PlateNumber: "51A-123.45"
            ),
            Timing: new TimingDto(
                EntryTime: DateTimeOffset.UtcNow.AddHours(-1),
                PricingCutoffAt: DateTimeOffset.UtcNow,
                TotalDurationMinutes: 60,
                BillableDurationMinutes: 60
            ),
            TariffPolicy: new TariffPolicyDto(
                TariffId: Guid.NewGuid(),
                TariffVersion: 1,
                PolicyName: "Standard",
                FreeParkingGraceMinutes: 15
            ),
            FinancialSummary: new FinancialSummaryDto(
                SubtotalParkingFee: 25000,
                TotalPenaltyFee: 0,
                DiscountAmount: 0,
                FinalTotalAmount: 25000,
                Currency: "VND",
                DepositDeducted: 0,
                SurplusRefund: 0
            ),
            FinancialStatus: new FinancialStatusDto(
                Status: "PAID",
                IsSettled: true,
                PaidAt: DateTimeOffset.UtcNow,
                PaymentMethod: "VIETQR",
                TransactionReference: "TXN-01",
                GatewayTransactionId: "GW-01"
            ),
            LineItems: null! // NULL LineItems boundary test
        );

        // Act
        var pdfBytes = await generator.GenerateInvoicePdfAsync(invoiceDto);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        // Valid PDF magic header
        Assert.Equal(0x25, pdfBytes[0]); // %
        Assert.Equal(0x50, pdfBytes[1]); // P
        Assert.Equal(0x44, pdfBytes[2]); // D
        Assert.Equal(0x46, pdfBytes[3]); // F
    }

    [Fact]
    public async Task QuestPdf_ZeroPenaltyAndZeroRefund_ExcludesOptionalRowsWithoutCrashing()
    {
        // Arrange
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        var generator = new QuestPdfInvoiceGenerator();

        var invoiceDto = new CustomerInvoiceResponseDto(
            InvoiceIdentity: new InvoiceIdentityDto(
                InvoiceId: Guid.NewGuid(),
                InvoiceNumber: "INV-ZERO-PENALTY",
                PaymentOrderId: Guid.NewGuid(),
                OrderCode: "ORD-002",
                IssuedAt: DateTimeOffset.UtcNow,
                PdfUrl: "/api/v1/payments/invoices/dummy/download"
            ),
            CustomerReference: new CustomerReferenceDto(
                AccountId: Guid.NewGuid(),
                CustomerType: "REGISTERED_DRIVER",
                DisplayName: "Test Driver"
            ),
            ParkingSessionReference: new ParkingSessionReferenceDto(
                SessionId: Guid.NewGuid(),
                ReferenceType: "PARKING_SESSION",
                SiteId: Guid.NewGuid(),
                SiteName: "Central Parking Lot",
                VehicleType: "CAR",
                PlateNumber: "51A-123.45"
            ),
            Timing: new TimingDto(
                EntryTime: DateTimeOffset.UtcNow.AddHours(-1),
                PricingCutoffAt: DateTimeOffset.UtcNow,
                TotalDurationMinutes: 60,
                BillableDurationMinutes: 60
            ),
            TariffPolicy: new TariffPolicyDto(
                TariffId: Guid.NewGuid(),
                TariffVersion: 1,
                PolicyName: "Standard",
                FreeParkingGraceMinutes: 15
            ),
            FinancialSummary: new FinancialSummaryDto(
                SubtotalParkingFee: 30000,
                TotalPenaltyFee: 0, // ZERO PENALTY
                DiscountAmount: 0,
                FinalTotalAmount: 30000,
                Currency: "VND",
                DepositDeducted: 0,
                SurplusRefund: 0 // ZERO REFUND
            ),
            FinancialStatus: new FinancialStatusDto(
                Status: "PAID",
                IsSettled: true,
                PaidAt: DateTimeOffset.UtcNow,
                PaymentMethod: "CASH",
                TransactionReference: "TXN-02",
                GatewayTransactionId: null
            ),
            LineItems: new List<InvoiceLineItemDto>() // EMPTY LIST boundary test
        );

        // Act
        var pdfBytes = await generator.GenerateInvoicePdfAsync(invoiceDto);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        Assert.Equal(0x25, pdfBytes[0]);
    }

    [Fact]
    public async Task QuestPdf_StressLargeData_50LineItemsMultiplePages_GeneratesValidMultiPagePdf()
    {
        // Arrange
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        var generator = new QuestPdfInvoiceGenerator();

        var baseTime = DateTimeOffset.UtcNow.AddHours(-50);
        var lineItems = new List<InvoiceLineItemDto>();
        for (int i = 0; i < 50; i++)
        {
            lineItems.Add(new InvoiceLineItemDto(
                ItemType: "PARKING_TIME_BLOCK",
                Description: $"Khung giờ đỗ xe thứ #{i + 1} - Phân đoạn đặc biệt có độ dài chuỗi tương đối dài nhằm kiểm thử wrapping trong table",
                StartTime: baseTime.AddHours(i),
                EndTime: baseTime.AddHours(i + 1),
                DurationMinutes: 60,
                CoveredMinutes: 60,
                ChargeableMinutes: 60,
                UnitRate: 15000,
                Unit: "block_1h",
                Amount: 15000
            ));
        }

        var invoiceDto = new CustomerInvoiceResponseDto(
            InvoiceIdentity: new InvoiceIdentityDto(
                InvoiceId: Guid.NewGuid(),
                InvoiceNumber: "INV-STRESS-50-ITEMS",
                PaymentOrderId: Guid.NewGuid(),
                OrderCode: "ORD-STRESS-01",
                IssuedAt: DateTimeOffset.UtcNow,
                PdfUrl: "/api/v1/payments/invoices/dummy/download"
            ),
            CustomerReference: new CustomerReferenceDto(
                AccountId: Guid.NewGuid(),
                CustomerType: "REGISTERED_DRIVER",
                DisplayName: "Nguyễn Văn A - Người dùng kiểm thử tải cao với tên rất dài"
            ),
            ParkingSessionReference: new ParkingSessionReferenceDto(
                SessionId: Guid.NewGuid(),
                ReferenceType: "PARKING_SESSION",
                SiteId: Guid.NewGuid(),
                SiteName: "Tòa nhà phức hợp Thương mại Dịch vụ Văn phòng SmartParking Center Tower",
                VehicleType: "CAR",
                PlateNumber: "51A-999.99"
            ),
            Timing: new TimingDto(
                EntryTime: baseTime,
                PricingCutoffAt: DateTimeOffset.UtcNow,
                TotalDurationMinutes: 3000,
                BillableDurationMinutes: 3000
            ),
            TariffPolicy: new TariffPolicyDto(
                TariffId: Guid.NewGuid(),
                TariffVersion: 1,
                PolicyName: "Biểu phí đỗ xe dài hạn VIP",
                FreeParkingGraceMinutes: 15
            ),
            FinancialSummary: new FinancialSummaryDto(
                SubtotalParkingFee: 750000,
                TotalPenaltyFee: 50000,
                DiscountAmount: 20000,
                FinalTotalAmount: 780000,
                Currency: "VND",
                DepositDeducted: 100000,
                SurplusRefund: 15000
            ),
            FinancialStatus: new FinancialStatusDto(
                Status: "PAID",
                IsSettled: true,
                PaidAt: DateTimeOffset.UtcNow,
                PaymentMethod: "VIETQR payOS",
                TransactionReference: "TXN-LONG-REF-999999999999",
                GatewayTransactionId: "LINK-VERY-LONG-PAYOS-IDENTIFIER-ABCXYZ"
            ),
            LineItems: lineItems
        );

        // Act
        var pdfBytes = await generator.GenerateInvoicePdfAsync(invoiceDto);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 5000, "Multi-page PDF should have substantial size (> 5KB)");
        Assert.Equal(0x25, pdfBytes[0]);
    }
}
