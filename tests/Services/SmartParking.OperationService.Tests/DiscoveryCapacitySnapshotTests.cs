using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Tests;

public sealed class DiscoveryCapacitySnapshotTests : IReservationPolicyRepository
{
    private Func<CancellationToken, Task>? duringPolicyRead;

    [QuoteDatabaseFact]
    public async Task FacilityPhysicalStateAndLocatedSessionMustAgreeForExactCount()
    {
        await using var services = Services();
        using var scope = services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IDiscoveryCapacityReader>();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var tenant = Guid.NewGuid();
        var site = Guid.NewGuid();
        var slot = Guid.NewGuid();
        var occupied = new HashSet<Guid> { slot };
        var free = new HashSet<Guid>();
        async Task<int?> Count(IReadOnlySet<Guid> physical) =>
            (await reader.ReadCarsAsync(tenant, site, [slot], null, default, physical)).AvailableCount;
        Assert.Null(await Count(occupied));
        Assert.Equal(1, await Count(free));
        var now = DateTimeOffset.UtcNow;
        db.ParkingSessions.Add(new ParkingSessionRow { Id = Guid.NewGuid(), SiteId = site, TenantId = tenant,
            CurrentSlotId = slot, LocationScannedAt = now, EntryTime = now, CreatedAt = now, VehicleType = "CAR",
            Version = 1, SessionStatus = "ACTIVE" });
        await db.SaveChangesAsync();
        Assert.Null(await Count(free));
        Assert.Equal(0, await Count(occupied));
    }

    private ServiceProvider Services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Reservation"] = Environment.GetEnvironmentVariable("SMARTPARK_QUOTE_TEST_DB")
        }).Build();
        var services = new ServiceCollection().AddPersistenceServices(config);
        services.AddSingleton<IReservationPolicyRepository>(this);
        return services.BuildServiceProvider();
    }

    public async Task<IReadOnlyList<ReservationPolicyRevision>> ReadEffectiveAsync(Guid tenantId, Guid siteId,
        DateTimeOffset at, CancellationToken ct)
    {
        if (duringPolicyRead is not null) await duringPolicyRead(ct);
        var values = ReservationPolicyResolver.RequiredKeys.ToDictionary(key => key, _ => 1);
        values["MAX_ADVANCE_BOOKING"] = 1440;
        values["RESERVATION_MAX_DURATION"] = 180;
        values["INTER_BOOKING_BUFFER"] = 5;
        values["ARRIVAL_LATE_TOLERANCE"] = 15;
        return [new(Guid.NewGuid(), 1, null, null, values, new Dictionary<string, PolicyBound>(), null, "Asia/Ho_Chi_Minh"),
            new(Guid.NewGuid(), 1, tenantId, siteId, new Dictionary<string, int>(), new Dictionary<string, PolicyBound>(), false, null)];
    }

    [QuoteDatabaseFact]
    public async Task ReaderUsesOneReadOnlySnapshotWhileAnotherWriterCommitsQuota()
    {
        await using var services = Services();
        using var readScope = services.CreateScope();
        using var writeScope = services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var writer = writeScope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var reader = readScope.ServiceProvider.GetRequiredService<IDiscoveryCapacityReader>();
        var site = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var slot = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var window = new ReservationInterval(now.AddHours(1), now.AddHours(2));
        duringPolicyRead = async ct =>
        {
            var txMode = await readDb.Database.SqlQuery<string>(
                $"SELECT current_setting('transaction_isolation') || '/' || current_setting('transaction_read_only') AS \"Value\"")
                .SingleAsync(ct);
            Assert.Equal("repeatable read/on", txMode);
            writer.Add(new ReservationQuotaConfig { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site,
                VehicleType = "CAR", IsActive = true, MaxReservationQuota = 0, CreatedAt = now, UpdatedAt = now });
            await writer.SaveChangesAsync(ct);
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var before = await reader.ReadCarsAsync(tenant, site, [slot], window, deadline.Token);
        Assert.Equal(1, before.AvailableCount);
        Assert.InRange(before.AsOf, now.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Null(readDb.Database.CurrentTransaction);
        Assert.Empty(readDb.ChangeTracker.Entries());

        duringPolicyRead = null;
        Assert.Equal(0, (await reader.ReadCarsAsync(tenant, site, [slot], window, default)).AvailableCount);
        duringPolicyRead = _ => throw new InvalidOperationException("Physical mode must skip advance-booking policy.");
        Assert.Equal(1, (await reader.ReadCarsAsync(tenant, site, [slot], null, default)).AvailableCount);
        Assert.False(await writer.Reservations.AnyAsync(r => r.SiteId == site));
        Assert.False(await writer.ParkingSessions.AnyAsync(s => s.SiteId == site));
        Assert.False(await writer.SlotAllocations.AnyAsync(a => a.SiteId == site));
    }

    [QuoteDatabaseFact]
    public async Task IntervalLoadsClaimsInProspectiveBufferAndLeavesWriterReadsGuarded()
    {
        await using var services = Services();
        using var scope = services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IDiscoveryCapacityReader>();
        var guarded = scope.ServiceProvider.GetRequiredService<IReservationAvailabilityRepository>();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var site = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var slot = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var window = new ReservationInterval(now.AddHours(1), now.AddHours(2));
        var row = new ReservationRow { Id = Guid.NewGuid(), SiteId = site, TenantId = tenant,
            VehicleType = "CAR", AccountId = Guid.NewGuid(), TargetSlotId = slot, Status = "CONFIRMED",
            ExpectedStartTime = window.End.AddMinutes(4), ExpectedEndTime = window.End.AddHours(1), CreatedAt = now };
        db.Add(row);
        await db.SaveChangesAsync();
        var result = await reader.ReadCarsAsync(tenant, site, [slot, Guid.NewGuid()], window, default);
        Assert.Equal(1, result.AvailableCount);
        Assert.Equal(1, (await reader.ReadCarsAsync(tenant, site, [slot], null, default)).AvailableCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.ReadAsync(row, now, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => guarded.ReadPhysicalAsync(
            new ParkingSessionRow { SiteId = site, TenantId = tenant }, now, default));
    }
}
