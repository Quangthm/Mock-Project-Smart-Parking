using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Usecase.PrepareParkingFeeOrder;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests.Application;

public class PrepareParkingFeeOrderCommandHandlerTests
{
    private readonly IParkingPricingCalculator _calculator = new ParkingPricingCalculator();
    private readonly FakePaymentOrderRepository _orderRepo = new();
    private readonly FakeInvoiceRepository _invoiceRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly MockTariffPort _tariffPort = new();

    private sealed class MockTariffPort : IParkingTariffPort
    {
        public PricingPolicySnapshot? PolicyToReturn { get; set; } = new(
            TariffId: Guid.NewGuid(),
            TariffName: "Biểu giá Ô tô Tiêu chuẩn Ngày/Đêm",
            PolicyVersion: 2,
            VehicleType: PricingVehicleType.CAR,
            FreeParkingGraceMinutes: 15,
            TimeBlocks: new List<TimeBlockRule>
            {
                new("DAY_TIME", new TimeOnly(6, 0), new TimeOnly(22, 0), 15000L),
                new("NIGHT_TIME", new TimeOnly(22, 0), new TimeOnly(6, 0), 30000L)
            },
            TimeZoneId: "Asia/Ho_Chi_Minh",
            RoundingMode: MonetaryRoundingMode.HALF_UP_THOUSAND
        );

        public Task<PricingPolicySnapshot?> GetActivePolicyAsync(Guid tenantId, Guid siteId, PricingVehicleType vehicleType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PolicyToReturn);
        }
    }

    private PrepareParkingFeeOrderCommandHandler CreateHandler(ICurrentActor? actor = null)
    {
        return new PrepareParkingFeeOrderCommandHandler(
            _tariffPort,
            _calculator,
            _orderRepo,
            _invoiceRepo,
            _uow,
            NullLogger<PrepareParkingFeeOrderCommandHandler>.Instance,
            actor ?? new FakeCurrentActor { IsInternalService = true }
        );
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesOrderAndInvoice_ReturnsExpectedDto()
    {
        var handler = CreateHandler();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var entryTime = DateTimeOffset.Parse("2026-11-02T08:00:00+07:00");
        var cutoffTime = DateTimeOffset.Parse("2026-11-02T11:30:00+07:00");

        var command = new PrepareParkingFeeOrderCommand(
            TenantId: tenantId,
            SiteId: siteId,
            SessionId: sessionId,
            VehicleType: "CAR",
            PlateNumber: "51A-123.45",
            EntryTime: entryTime,
            PricingCutoffAt: cutoffTime,
            AccountId: Guid.NewGuid(),
            PenaltyAmount: 20000,
            PenaltyReason: "Đỗ quá hạn đặt chỗ 45 phút",
            IdempotencyKey: $"checkout:session:{sessionId}"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal("PENDING", result.Status);
        Assert.True(result.BaseParkingFee > 0);
        Assert.Equal(20000, result.PenaltyFee);
        Assert.Equal(result.BaseParkingFee + 20000, result.FinalAmount);

        // Verify state in repository
        var order = await _orderRepo.GetByIdAsync(result.OrderId);
        Assert.NotNull(order);
        Assert.Equal(PaymentOrderStatus.PENDING, order.Status);
        Assert.Equal(result.FinalAmount, order.FinalAmount);

        var invoice = await _invoiceRepo.GetByPaymentOrderIdAsync(order.Id);
        Assert.NotNull(invoice);
        Assert.StartsWith("INV-", invoice.InvoiceNumber);
        Assert.Equal(result.FinalAmount, invoice.FinalAmount);
        Assert.Equal(2, invoice.PricingSnapshot.LineItems.Count); // Parking time block + penalty
        Assert.Equal("51A-123.45", invoice.PricingSnapshot.PlateNumber);
        Assert.Equal("REGISTERED_DRIVER", invoice.PricingSnapshot.CustomerType);
    }

    [Fact]
    public async Task Handle_InvalidCutoffTime_ThrowsPaymentDomainException()
    {
        var handler = CreateHandler();
        var entryTime = DateTimeOffset.UtcNow;
        var cutoffTime = entryTime.AddMinutes(-30); // Cutoff before entry!

        var command = new PrepareParkingFeeOrderCommand(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            VehicleType: "CAR",
            PlateNumber: "51A-123.45",
            EntryTime: entryTime,
            PricingCutoffAt: cutoffTime,
            AccountId: null,
            PenaltyAmount: null,
            PenaltyReason: null,
            IdempotencyKey: "test-invalid-cutoff"
        );

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(PaymentDomainException.InvalidCutoffTime, ex.Code);
    }

    [Fact]
    public async Task Handle_IdempotentReplay_ReturnsExistingOrderWithoutDuplicates()
    {
        var handler = CreateHandler();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var key = $"checkout:session:{sessionId}";

        var command = new PrepareParkingFeeOrderCommand(
            TenantId: tenantId,
            SiteId: siteId,
            SessionId: sessionId,
            VehicleType: "CAR",
            PlateNumber: "51A-123.45",
            EntryTime: DateTimeOffset.UtcNow.AddHours(-2),
            PricingCutoffAt: DateTimeOffset.UtcNow,
            AccountId: null,
            PenaltyAmount: null,
            PenaltyReason: null,
            IdempotencyKey: key
        );

        var result1 = await handler.Handle(command, CancellationToken.None);
        var result2 = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(result1.OrderId, result2.OrderId);
        Assert.Equal(result1.OrderCode, result2.OrderCode);
        Assert.Single(_orderRepo.Orders);
        Assert.Single(_invoiceRepo.Invoices);
    }

    [Fact]
    public async Task Handle_IdempotencyMismatch_ThrowsPaymentDomainException()
    {
        var handler = CreateHandler();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var key = "same-idempotency-key";

        var command1 = new PrepareParkingFeeOrderCommand(
            TenantId: tenantId,
            SiteId: siteId,
            SessionId: sessionId,
            VehicleType: "CAR",
            PlateNumber: "51A-123.45",
            EntryTime: DateTimeOffset.UtcNow.AddHours(-2),
            PricingCutoffAt: DateTimeOffset.UtcNow,
            AccountId: null,
            PenaltyAmount: null,
            PenaltyReason: null,
            IdempotencyKey: key
        );

        await handler.Handle(command1, CancellationToken.None);

        var command2 = new PrepareParkingFeeOrderCommand(
            TenantId: Guid.NewGuid(), // Different Tenant!
            SiteId: siteId,
            SessionId: sessionId,
            VehicleType: "CAR",
            PlateNumber: "51A-123.45",
            EntryTime: DateTimeOffset.UtcNow.AddHours(-2),
            PricingCutoffAt: DateTimeOffset.UtcNow,
            AccountId: null,
            PenaltyAmount: null,
            PenaltyReason: null,
            IdempotencyKey: key
        );

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command2, CancellationToken.None));
        Assert.Equal(PaymentDomainException.IdempotencyPayloadMismatch, ex.Code);
    }

    [Fact]
    public async Task Handle_TariffNotFound_ThrowsPaymentDomainException()
    {
        _tariffPort.PolicyToReturn = null; // No tariff found
        var handler = CreateHandler();

        var command = new PrepareParkingFeeOrderCommand(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            VehicleType: "CAR",
            PlateNumber: "51A-123.45",
            EntryTime: DateTimeOffset.UtcNow.AddHours(-2),
            PricingCutoffAt: DateTimeOffset.UtcNow,
            AccountId: null,
            PenaltyAmount: null,
            PenaltyReason: null,
            IdempotencyKey: "test-no-tariff"
        );

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(PaymentDomainException.TariffNotFound, ex.Code);
    }

    [Fact]
    public void Validator_InvalidModel_FailsValidation()
    {
        var validator = new PrepareParkingFeeOrderCommandValidator();

        var command = new PrepareParkingFeeOrderCommand(
            TenantId: Guid.Empty,
            SiteId: Guid.Empty,
            SessionId: Guid.Empty,
            VehicleType: "AIRPLANE", // Invalid
            PlateNumber: "",
            EntryTime: default,
            PricingCutoffAt: default,
            AccountId: null,
            PenaltyAmount: -500, // Invalid
            PenaltyReason: null,
            IdempotencyKey: ""
        );

        var validationResult = validator.Validate(command);
        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "TenantId");
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "SiteId");
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "SessionId");
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "VehicleType");
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "PlateNumber");
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "EntryTime");
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "PricingCutoffAt");
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "PenaltyAmount");
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "IdempotencyKey");
    }

    [Fact]
    public async Task Handle_UnauthorizedCaller_ThrowsPaymentDomainExceptionUnauthorized()
    {
        var unauthActor = new FakeCurrentActor { IsInternalService = false, Role = "DRIVER" };
        var handler = CreateHandler(unauthActor);
        var command = new PrepareParkingFeeOrderCommand(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            VehicleType: "CAR",
            PlateNumber: "51A-123.45",
            EntryTime: DateTimeOffset.UtcNow.AddHours(-2),
            PricingCutoffAt: DateTimeOffset.UtcNow,
            AccountId: Guid.NewGuid(),
            PenaltyAmount: 0,
            PenaltyReason: null,
            IdempotencyKey: "test:auth:key"
        );

        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal(PaymentDomainException.Unauthorized, ex.ErrorCode);
    }
}
