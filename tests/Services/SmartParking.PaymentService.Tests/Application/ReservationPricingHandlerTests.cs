using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.CreateReservationQuote;
using SmartParking.PaymentService.Application.Usecase.Reservations.Queries.PreviewReservationPrice;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Application;

public class ReservationPricingHandlerTests
{
    private readonly IParkingPricingCalculator _calculator = new ParkingPricingCalculator();

    private static PricingPolicySnapshot CreateSamplePolicy(PricingVehicleType vehicleType = PricingVehicleType.CAR)
    {
        return new PricingPolicySnapshot(
            TariffId: Guid.NewGuid(),
            TariffName: "Biểu giá Đặt chỗ Chuẩn",
            PolicyVersion: 1,
            VehicleType: vehicleType,
            FreeParkingGraceMinutes: 15,
            TimeBlocks: new List<TimeBlockRule>
            {
                new("ALL_DAY", new TimeOnly(0, 0), new TimeOnly(0, 0), 20000L)
            },
            TimeZoneId: "Asia/Ho_Chi_Minh",
            RoundingMode: MonetaryRoundingMode.HALF_UP_WHOLE_VND
        );
    }

    private sealed class MockTariffPort : IParkingTariffPort
    {
        public PricingPolicySnapshot? PolicyToReturn { get; set; }
        public bool WasCalled { get; private set; }

        public Task<PricingPolicySnapshot?> GetActivePolicyAsync(Guid tenantId, Guid siteId, PricingVehicleType vehicleType, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(PolicyToReturn);
        }
    }

    [Fact]
    public async Task Preview_ValidRequest_CalculatesCorrectPriceInMemoryWithoutDb()
    {
        // Arrange
        var tariffPort = new MockTariffPort { PolicyToReturn = CreateSamplePolicy() };
        var handler = new PreviewReservationPriceQueryHandler(
            _calculator,
            tariffPort,
            NullLogger<PreviewReservationPriceQueryHandler>.Instance
        );

        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddHours(1);
        var end = start.AddHours(2); // 2 hours @ 20,000 VND = 40,000 VND

        var requestDto = new ReservationPricePreviewRequestDto(
            SiteId: siteId,
            RequestedSlotId: slotId,
            Start: start,
            End: end,
            VehicleId: Guid.NewGuid(),
            PaymentMethod: "PAYOS"
        );

        // Act
        var result = await handler.Handle(new PreviewReservationPriceQuery(requestDto), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(40000m, result.BookedAmount);
        Assert.Equal(40000m, result.DepositAmount);
        Assert.Equal("VND", result.Currency);
        Assert.Equal("1", result.PricingVersion);
        Assert.False(result.RequiresCompleteSelection);
        Assert.True(tariffPort.WasCalled);
    }

    [Fact]
    public async Task Preview_MissingVehicleOrPaymentMethod_SetsRequiresCompleteSelectionTrue()
    {
        // Arrange
        var tariffPort = new MockTariffPort { PolicyToReturn = CreateSamplePolicy() };
        var handler = new PreviewReservationPriceQueryHandler(
            _calculator,
            tariffPort,
            NullLogger<PreviewReservationPriceQueryHandler>.Instance
        );

        var requestDto = new ReservationPricePreviewRequestDto(
            SiteId: Guid.NewGuid(),
            RequestedSlotId: Guid.NewGuid(),
            Start: DateTimeOffset.UtcNow.AddHours(1),
            End: DateTimeOffset.UtcNow.AddHours(2),
            VehicleId: null,
            PaymentMethod: null
        );

        // Act
        var result = await handler.Handle(new PreviewReservationPriceQuery(requestDto), CancellationToken.None);

        // Assert
        Assert.True(result.RequiresCompleteSelection);
    }

    [Fact]
    public async Task Preview_NoTariffFound_ThrowsPricingDomainException()
    {
        // Arrange
        var tariffPort = new MockTariffPort { PolicyToReturn = null };
        var handler = new PreviewReservationPriceQueryHandler(
            _calculator,
            tariffPort,
            NullLogger<PreviewReservationPriceQueryHandler>.Instance
        );

        var requestDto = new ReservationPricePreviewRequestDto(
            SiteId: Guid.NewGuid(),
            RequestedSlotId: Guid.NewGuid(),
            Start: DateTimeOffset.UtcNow.AddHours(1),
            End: DateTimeOffset.UtcNow.AddHours(2)
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PricingDomainException>(() =>
            handler.Handle(new PreviewReservationPriceQuery(requestDto), CancellationToken.None));
        Assert.Equal("TARIFF_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task CreateQuote_ValidRequest_CreatesAndPersistsQuoteSnapshot()
    {
        // Arrange
        var tariffPort = new MockTariffPort { PolicyToReturn = CreateSamplePolicy() };
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();
        var actor = new FakeCurrentActor { AccountId = Guid.NewGuid() };

        var handler = new CreateReservationQuoteCommandHandler(
            actor,
            _calculator,
            tariffPort,
            quoteRepo,
            uow,
            NullLogger<CreateReservationQuoteCommandHandler>.Instance
        );

        var siteId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddHours(1);
        var end = start.AddHours(3); // 3 hours @ 20,000 VND = 60,000 VND

        var requestDto = new CreateReservationQuoteRequestDto(
            SiteId: siteId,
            RequestedSlotId: slotId,
            Start: start,
            End: end,
            VehicleId: vehicleId,
            PaymentMethod: "PAYOS",
            PrepaymentMode: "FULL"
        );

        // Act
        var result = await handler.Handle(new CreateReservationQuoteCommand(requestDto), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.QuoteId);
        Assert.Equal(60000m, result.BookedAmount);
        Assert.Equal(60000m, result.DepositAmount);
        Assert.Equal("FULL", result.PrepaymentMode);
        Assert.Equal("VND", result.Currency);
        Assert.Equal(actor.AccountId.Value, result.AccountId);
        Assert.True(result.ValidUntil > DateTimeOffset.UtcNow);

        // Assert DB
        Assert.Single(quoteRepo.Quotes);
        var savedQuote = quoteRepo.Quotes[0];
        Assert.Equal(result.QuoteId, savedQuote.Id);
        Assert.Equal(ReservationQuoteStatus.ISSUED, savedQuote.Status);
        Assert.Equal(60000m, savedQuote.BookedAmount);
        Assert.Equal(60000m, savedQuote.DepositAmount);
        Assert.Equal(PrepaymentMode.FULL, savedQuote.PrepaymentMode);
        Assert.True(uow.SaveCount > 0);
    }

    [Fact]
    public async Task CreateQuote_Unauthenticated_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var tariffPort = new MockTariffPort { PolicyToReturn = CreateSamplePolicy() };
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();
        var actor = new FakeCurrentActor { AccountId = null };

        var handler = new CreateReservationQuoteCommandHandler(
            actor,
            _calculator,
            tariffPort,
            quoteRepo,
            uow,
            NullLogger<CreateReservationQuoteCommandHandler>.Instance
        );

        var requestDto = new CreateReservationQuoteRequestDto(
            SiteId: Guid.NewGuid(),
            RequestedSlotId: Guid.NewGuid(),
            Start: DateTimeOffset.UtcNow.AddHours(1),
            End: DateTimeOffset.UtcNow.AddHours(3),
            VehicleId: Guid.NewGuid(),
            PaymentMethod: "PAYOS",
            AccountId: null // neither actor nor request has AccountId
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new CreateReservationQuoteCommand(requestDto), CancellationToken.None));
        Assert.Equal(PaymentDomainException.Unauthorized, ex.Code);
        Assert.Empty(quoteRepo.Quotes);
    }

    [Theory]
    [InlineData("FULL", 60000, 60000)]
    [InlineData("PERCENTAGE", 60000, 30000)]
    [InlineData("FIXED", 60000, 50000)]
    public async Task CreateQuote_DifferentPrepaymentModes_CalculatesDepositAccordingly(
        string mode, decimal expectedBooked, decimal expectedDeposit)
    {
        // Arrange
        var tariffPort = new MockTariffPort { PolicyToReturn = CreateSamplePolicy() };
        var quoteRepo = new FakeReservationQuoteRepository();
        var uow = new FakeUnitOfWork();
        var actor = new FakeCurrentActor { AccountId = Guid.NewGuid() };

        var handler = new CreateReservationQuoteCommandHandler(
            actor,
            _calculator,
            tariffPort,
            quoteRepo,
            uow,
            NullLogger<CreateReservationQuoteCommandHandler>.Instance
        );

        var start = DateTimeOffset.UtcNow.AddHours(1);
        var end = start.AddHours(3);

        var requestDto = new CreateReservationQuoteRequestDto(
            SiteId: Guid.NewGuid(),
            RequestedSlotId: Guid.NewGuid(),
            Start: start,
            End: end,
            VehicleId: Guid.NewGuid(),
            PaymentMethod: "PAYOS",
            PrepaymentMode: mode
        );

        // Act
        var result = await handler.Handle(new CreateReservationQuoteCommand(requestDto), CancellationToken.None);

        // Assert
        Assert.Equal(expectedBooked, result.BookedAmount);
        Assert.Equal(expectedDeposit, result.DepositAmount);
        Assert.Equal(mode, result.PrepaymentMode);
    }
}
