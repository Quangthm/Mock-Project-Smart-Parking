using QuestPDF.Infrastructure;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Infrastructure.Adapters;
using Xunit;

namespace SmartParking.PaymentService.Tests.Infrastructure;

public class QuestPdfInvoiceGeneratorTests
{
    private readonly QuestPdfInvoiceGenerator _generator;

    public QuestPdfInvoiceGeneratorTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        _generator = new QuestPdfInvoiceGenerator();
    }

    private static CustomerInvoiceResponseDto CreateSampleDto(
        bool hasPenalty = false,
        decimal depositDeducted = 0,
        decimal surplusRefund = 0,
        Guid? accountId = null,
        string? displayName = "Nguyen Van A")
    {
        var invoiceId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow;
        var fee = 45000m;
        var penalty = hasPenalty ? 20000m : 0m;
        var finalTotal = fee + penalty - depositDeducted;

        var lineItems = new List<InvoiceLineItemDto>
        {
            new(
                ItemType: "PARKING_TIME_BLOCK",
                Description: "Ca ngày (06:00 - 18:00)",
                StartTime: issuedAt.AddHours(-3),
                EndTime: issuedAt,
                DurationMinutes: 180,
                CoveredMinutes: 180,
                ChargeableMinutes: 180,
                UnitRate: 15000m,
                Unit: "block_1h",
                Amount: fee
            )
        };

        if (hasPenalty)
        {
            lineItems.Add(new(
                ItemType: "PENALTY",
                Description: "Phạt quá giờ (Overstay 45p)",
                StartTime: issuedAt.AddMinutes(-45),
                EndTime: issuedAt,
                DurationMinutes: 45,
                CoveredMinutes: 0,
                ChargeableMinutes: 45,
                UnitRate: 20000m,
                Unit: "incident",
                Amount: penalty
            ));
        }

        return new CustomerInvoiceResponseDto(
            InvoiceIdentity: new InvoiceIdentityDto(
                InvoiceId: invoiceId,
                InvoiceNumber: "INV-20261110-0001",
                PaymentOrderId: orderId,
                OrderCode: "ORD-20261110-01",
                IssuedAt: issuedAt,
                PdfUrl: $"/api/v1/payments/invoices/{invoiceId}/download"
            ),
            CustomerReference: new CustomerReferenceDto(
                AccountId: accountId ?? Guid.NewGuid(),
                CustomerType: accountId.HasValue ? "REGISTERED_DRIVER" : "GUEST_DRIVER",
                DisplayName: displayName
            ),
            ParkingSessionReference: new ParkingSessionReferenceDto(
                SessionId: Guid.NewGuid(),
                ReferenceType: "PARKING_SESSION",
                SiteId: Guid.NewGuid(),
                SiteName: "Landmark 81 Central Parking",
                VehicleType: "CAR",
                PlateNumber: "51A-123.45"
            ),
            Timing: new TimingDto(
                EntryTime: issuedAt.AddHours(-3),
                PricingCutoffAt: issuedAt,
                TotalDurationMinutes: 180,
                BillableDurationMinutes: 180
            ),
            TariffPolicy: new TariffPolicyDto(
                TariffId: Guid.NewGuid(),
                TariffVersion: 1,
                PolicyName: "Bảng cước Ô tô Tiêu chuẩn",
                FreeParkingGraceMinutes: 15
            ),
            FinancialSummary: new FinancialSummaryDto(
                SubtotalParkingFee: fee,
                TotalPenaltyFee: penalty,
                DiscountAmount: 0,
                FinalTotalAmount: finalTotal,
                Currency: "VND",
                DepositDeducted: depositDeducted,
                SurplusRefund: surplusRefund
            ),
            FinancialStatus: new FinancialStatusDto(
                Status: "PAID",
                IsSettled: true,
                PaidAt: issuedAt,
                PaymentMethod: "VIETQR payOS",
                TransactionReference: "TXN-20261110001",
                GatewayTransactionId: "LINK-9999"
            ),
            LineItems: lineItems
        );
    }

    [Fact]
    public async Task GenerateInvoicePdfAsync_StandardSettledInvoice_ReturnsValidNonEmptyPdfBytesWithMagicHeader()
    {
        // Arrange
        var dto = CreateSampleDto();

        // Act
        var pdfBytes = await _generator.GenerateInvoicePdfAsync(dto);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000, "PDF size should be greater than 1000 bytes");

        // Verify PDF Magic Bytes (%PDF)
        Assert.Equal(0x25, pdfBytes[0]); // '%'
        Assert.Equal(0x50, pdfBytes[1]); // 'P'
        Assert.Equal(0x44, pdfBytes[2]); // 'D'
        Assert.Equal(0x46, pdfBytes[3]); // 'F'
    }

    [Fact]
    public async Task GenerateInvoicePdfAsync_WithPenaltyAndDepositDeduction_RendersAllFinancialLinesCorrectly()
    {
        // Arrange
        var dto = CreateSampleDto(hasPenalty: true, depositDeducted: 10000m, surplusRefund: 5000m);

        // Act
        var pdfBytes = await _generator.GenerateInvoicePdfAsync(dto);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        Assert.Equal(0x25, pdfBytes[0]);
    }

    [Fact]
    public async Task GenerateInvoicePdfAsync_WithMultipleLineItems_RendersMultiRowTableSuccessfully()
    {
        // Arrange
        var baseDto = CreateSampleDto();
        var extraItems = new List<InvoiceLineItemDto>(baseDto.LineItems)
        {
            new(
                ItemType: "PARKING_TIME_BLOCK",
                Description: "Ca đêm (18:00 - 22:00)",
                StartTime: baseDto.Timing.EntryTime.AddHours(3),
                EndTime: baseDto.Timing.PricingCutoffAt,
                DurationMinutes: 60,
                CoveredMinutes: 60,
                ChargeableMinutes: 60,
                UnitRate: 20000m,
                Unit: "block_1h",
                Amount: 20000m
            )
        };

        var multiItemDto = baseDto with { LineItems = extraItems };

        // Act
        var pdfBytes = await _generator.GenerateInvoicePdfAsync(multiItemDto);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
    }

    [Fact]
    public async Task GenerateInvoicePdfAsync_GuestDriverWithNullAccountAndDisplayName_RendersGracefullyWithoutException()
    {
        // Arrange
        var guestDto = CreateSampleDto(accountId: null, displayName: null);

        // Act
        var pdfBytes = await _generator.GenerateInvoicePdfAsync(guestDto);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
    }
}
