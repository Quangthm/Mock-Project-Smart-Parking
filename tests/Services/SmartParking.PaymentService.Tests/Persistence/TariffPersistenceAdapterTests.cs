using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Adapters;

namespace SmartParking.PaymentService.Tests.Persistence;

public class TariffPersistenceAdapterTests
{
    private static PaymentDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task GetActivePolicyAsync_WhenTariffExists_ReturnsMappedPricingPolicySnapshot()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var tariffId = Guid.NewGuid();

        var tariff = new Tariff
        {
            Id = tariffId,
            TenantId = tenantId,
            SiteId = siteId,
            Name = "Biểu phí v1.0 Standard",
            VehicleType = "CAR",
            FreeParkingGraceMinutes = 15,
            TariffRulesJson = """
            {
              "timeZoneId": "Asia/Ho_Chi_Minh",
              "roundingMode": "HALF_UP_THOUSAND",
              "timeBlocks": [
                {
                  "blockCode": "DAY",
                  "startTime": "06:00:00",
                  "endTime": "18:00:00",
                  "ratePerHourVnd": 20000
                },
                {
                  "blockCode": "NIGHT",
                  "startTime": "18:00:00",
                  "endTime": "06:00:00",
                  "ratePerHourVnd": 30000
                }
              ]
            }
            """,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var revision = new PolicyRevision
        {
            Id = Guid.NewGuid(),
            PolicyId = tariffId,
            PolicyType = "TARIFF",
            RevisionNumber = 3,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            ScopeType = "SITE",
            ScopeId = siteId,
            PolicyValuesJson = "{}",
            CreatedBy = Guid.NewGuid()
        };

        dbContext.Tariffs.Add(tariff);
        dbContext.PolicyRevisions.Add(revision);
        await dbContext.SaveChangesAsync();

        var adapter = new TariffPersistenceAdapter(dbContext, NullLogger<TariffPersistenceAdapter>.Instance);

        // Act
        var result = await adapter.GetActivePolicyAsync(tenantId, siteId, PricingVehicleType.CAR);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tariffId, result.TariffId);
        Assert.Equal("Biểu phí v1.0 Standard", result.TariffName);
        Assert.Equal(3, result.PolicyVersion);
        Assert.Equal(PricingVehicleType.CAR, result.VehicleType);
        Assert.Equal(15, result.FreeParkingGraceMinutes);
        Assert.Equal(MonetaryRoundingMode.HALF_UP_THOUSAND, result.RoundingMode);
        Assert.Equal(2, result.TimeBlocks.Count);
        Assert.Equal("DAY", result.TimeBlocks[0].BlockCode);
        Assert.Equal(20000L, result.TimeBlocks[0].RatePerHourVnd);
        Assert.Equal("NIGHT", result.TimeBlocks[1].BlockCode);
        Assert.Equal(30000L, result.TimeBlocks[1].RatePerHourVnd);
    }

    [Fact]
    public async Task GetActivePolicyAsync_WhenNoTariffExists_ReturnsNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var adapter = new TariffPersistenceAdapter(dbContext, NullLogger<TariffPersistenceAdapter>.Instance);

        // Act
        var result = await adapter.GetActivePolicyAsync(Guid.NewGuid(), Guid.NewGuid(), PricingVehicleType.MOTORCYCLE);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetActivePolicyAsync_WhenTariffIsSoftDeleted_ReturnsNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var tariff = new Tariff
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            Name = "Deleted Tariff",
            VehicleType = "CAR",
            FreeParkingGraceMinutes = 15,
            TariffRulesJson = "{}",
            IsActive = true,
            DeletedAt = DateTimeOffset.UtcNow
        };

        dbContext.Tariffs.Add(tariff);
        await dbContext.SaveChangesAsync();

        var adapter = new TariffPersistenceAdapter(dbContext, NullLogger<TariffPersistenceAdapter>.Instance);

        // Act
        var result = await adapter.GetActivePolicyAsync(tenantId, siteId, PricingVehicleType.CAR);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetActivePolicyAsync_WhenTariffIsInactive_ReturnsNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var tariff = new Tariff
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SiteId = siteId,
            Name = "Inactive Tariff",
            VehicleType = "CAR",
            FreeParkingGraceMinutes = 15,
            TariffRulesJson = "{}",
            IsActive = false
        };

        dbContext.Tariffs.Add(tariff);
        await dbContext.SaveChangesAsync();

        var adapter = new TariffPersistenceAdapter(dbContext, NullLogger<TariffPersistenceAdapter>.Instance);

        // Act
        var result = await adapter.GetActivePolicyAsync(tenantId, siteId, PricingVehicleType.CAR);

        // Assert
        Assert.Null(result);
    }
}
