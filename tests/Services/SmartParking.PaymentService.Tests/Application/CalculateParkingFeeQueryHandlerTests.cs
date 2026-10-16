using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Tests.Application;

public class CalculateParkingFeeQueryHandlerTests
{
    private readonly IParkingPricingCalculator _calculator = new ParkingPricingCalculator();

    private static PricingPolicySnapshot CreateSamplePolicy(PricingVehicleType vehicleType = PricingVehicleType.CAR)
    {
        return new PricingPolicySnapshot(
            TariffId: Guid.NewGuid(),
            TariffName: "Biểu giá Chuẩn",
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

    private sealed class MockReservationPort : IReservationPricingSnapshotPort
    {
        public PricingPolicySnapshot? SnapshotToReturn { get; set; }
        public bool WasCalled { get; private set; }

        public Task<PricingPolicySnapshot?> GetAcceptedPolicySnapshotAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(SnapshotToReturn);
        }
    }

    [Fact]
    public async Task Handle_WithPolicyOverride_UsesOverrideWithoutCallingPorts()
    {
        var tariffPort = new MockTariffPort();
        var reservationPort = new MockReservationPort();
        var handler = new CalculateParkingFeeQueryHandler(_calculator, tariffPort, reservationPort);

        var policyOverride = CreateSamplePolicy();
        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "CAR",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(2),
            PolicyOverride: policyOverride,
            IsTrustedInternalCaller: true
        );

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(tariffPort.WasCalled);
        Assert.False(reservationPort.WasCalled);
        Assert.Equal(40000L, result.FinalAmountVnd);
    }

    [Fact]
    public async Task Handle_WithValidReservationId_UsesSnapshotFromPort()
    {
        var tariffPort = new MockTariffPort();
        var reservationPort = new MockReservationPort { SnapshotToReturn = CreateSamplePolicy() };
        var handler = new CalculateParkingFeeQueryHandler(_calculator, tariffPort, reservationPort);

        var reservationId = Guid.NewGuid();
        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "CAR",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1),
            ReservationId: reservationId
        );

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(reservationPort.WasCalled);
        Assert.False(tariffPort.WasCalled);
        Assert.Equal(20000L, result.FinalAmountVnd);
    }

    [Fact]
    public async Task Handle_WithCorruptedOrMissingReservationSnapshot_ThrowsCorruptedPolicySnapshotException_AndNeverCallsTariffPort()
    {
        var tariffPort = new MockTariffPort();
        var reservationPort = new MockReservationPort { SnapshotToReturn = null }; // missing/corrupted
        var handler = new CalculateParkingFeeQueryHandler(_calculator, tariffPort, reservationPort);

        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "CAR",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1),
            ReservationId: Guid.NewGuid()
        );

        var ex = await Assert.ThrowsAsync<PricingDomainException>(() =>
            handler.Handle(query, CancellationToken.None));

        Assert.Equal("CORRUPTED_POLICY_SNAPSHOT", ex.Code);
        Assert.Equal("reservationId", ex.Field);
        Assert.True(reservationPort.WasCalled);
        Assert.False(tariffPort.WasCalled); // NEVER fall back to walk-in
    }

    [Fact]
    public async Task Handle_WalkInWithActiveTariff_CalculatesSuccessfully()
    {
        var tariffPort = new MockTariffPort { PolicyToReturn = CreateSamplePolicy() };
        var reservationPort = new MockReservationPort();
        var handler = new CalculateParkingFeeQueryHandler(_calculator, tariffPort, reservationPort);

        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "CAR",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1),
            ReservationId: null
        );

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(tariffPort.WasCalled);
        Assert.False(reservationPort.WasCalled);
        Assert.Equal(20000L, result.FinalAmountVnd);
    }

    [Fact]
    public async Task Handle_WalkInWithNoActiveTariff_ThrowsTariffNotFoundException()
    {
        var tariffPort = new MockTariffPort { PolicyToReturn = null }; // no tariff
        var reservationPort = new MockReservationPort();
        var handler = new CalculateParkingFeeQueryHandler(_calculator, tariffPort, reservationPort);

        var query = new CalculateParkingFeeQuery(
            TenantId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            VehicleType: "MOTORCYCLE",
            EntryTime: DateTimeOffset.UtcNow,
            ExitTime: DateTimeOffset.UtcNow.AddHours(1),
            ReservationId: null
        );

        var ex = await Assert.ThrowsAsync<PricingDomainException>(() =>
            handler.Handle(query, CancellationToken.None));

        Assert.Equal("TARIFF_NOT_FOUND", ex.Code);
        Assert.Equal("vehicleType", ex.Field);
    }
}
