using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationPlateGuardTests
{
    [ReservationTransportFact]
    public async Task RacingSlotsSerializeByPlateAndSiteWhileAnOpenVisitBlocksAcrossSites()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_DB")).Options;
        var site = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var otherSite = Guid.NewGuid();
        var plate = $"51A{Random.Shared.Next(10000, 100000)}";
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var end = start.AddHours(1);
        async Task<bool> Reserve(Guid targetSite, DateTimeOffset from)
        {
            await using var db = new ReservationDbContext(options);
            await using var tx = await db.Database.BeginTransactionAsync();
            var guards = new OperationsConcurrency(db);
            await guards.LockVehicleAsync(plate, default);
            await guards.LockSiteAsync(targetSite, default);
            var booking = new ReservationRow
            {
                Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), SiteId = targetSite, TenantId = tenant,
                TargetSlotId = Guid.NewGuid(), NormalizedPlate = plate, VehicleType = "CAR", Status = "CONFIRMED",
                ExpectedStartTime = from, ExpectedEndTime = from.AddHours(1), CreatedAt = DateTimeOffset.UtcNow
            };
            try
            {
                await new ReservationRepository(db).RequirePlateAvailableAsync(booking, DateTimeOffset.UtcNow, default);
            }
            catch (ReservationGateException error) when (error.Code == "VEHICLE_TIME_CONFLICT") { return false; }
            db.Add(booking);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }

        var winners = await Task.WhenAll(Reserve(site, start), Reserve(site, start));
        Assert.Single(winners, won => won);
        Assert.True(await Reserve(otherSite, start));
        Assert.True(await Reserve(site, end));

        await using var visits = new ReservationDbContext(options);
        await visits.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO parking_sessions (id, tenant_id, site_id, normalized_plate) VALUES ({Guid.NewGuid()}, {tenant}, {site}, {plate})");
        Assert.False(await Reserve(otherSite, end.AddDays(7)));
    }
}
