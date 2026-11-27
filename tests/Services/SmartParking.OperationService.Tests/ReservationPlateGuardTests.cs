using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationPlateGuardTests
{
    [QuoteDatabaseFact]
    public async Task SameSiteOverlapsSerializeAndPhysicalVisitsDoNotCreateReservationConflicts()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("SMARTPARK_QUOTE_TEST_DB")).Options;
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
        Assert.True(await Reserve(otherSite, end.AddDays(7)));
        Assert.False(await Reserve(site, start));
        var existing = await visits.Reservations.FirstAsync(r => r.SiteId == site && r.NormalizedPlate == plate);
        await visits.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE parking_sessions SET reservation_id = {existing.Id} WHERE site_id = {site} AND normalized_plate = {plate}");
        Assert.True(await Reserve(site, end.AddDays(8)));
        Assert.False(await Reserve(site, start));
        await visits.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE parking_sessions SET exit_time = entry_time + interval '1 minute' WHERE site_id = {site} AND normalized_plate = {plate}");
        Assert.True(await Reserve(site, end.AddDays(9)));
        Assert.False(await Reserve(site, start));
    }

    [QuoteDatabaseFact]
    public async Task PlatePredicateUsesAcceptedBufferAndStopsCountingUnpaidHoldAtH()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("SMARTPARK_QUOTE_TEST_DB")).Options;
        await using var db = new ReservationDbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync();
        var site = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        const string plate = "51A12345";
        var h = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(5);
        var start = h.AddDays(1);
        var end = start.AddHours(1);
        var bufferedEnd = end.AddMinutes(5);
        var id = Guid.NewGuid();
        var slot = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO reservations (id, tenant_id, site_id, account_id, normalized_plate, status,
                expected_start_time, expected_end_time, claim_end_time, hold_expires_at, target_slot_id)
            VALUES ({id}, {tenant}, {site}, {Guid.NewGuid()}, {plate}, 'PENDING_PAYMENT', {start}, {end}, {bufferedEnd}, {h}, {slot})
            """);
        var guards = new OperationsConcurrency(db);
        await guards.LockVehicleAsync(plate, default);
        await guards.LockSiteAsync(site, default);
        var proposed = new ReservationRow { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site, NormalizedPlate = plate, TargetSlotId = slot,
            ExpectedStartTime = end, ExpectedEndTime = end.AddHours(1) };
        var availability = new ReservationAvailabilityRepository(db, guards);
        Task Check(DateTimeOffset at) => new ReservationRepository(db).RequirePlateAvailableAsync(proposed, at, default);
        Assert.Equal("VEHICLE_TIME_CONFLICT",
            (await Assert.ThrowsAsync<ReservationGateException>(() => Check(h.AddSeconds(-1)))).Code);
        Assert.Equal(id, Assert.Single((await availability.ReadAsync(proposed, h.AddSeconds(-1), default)).Reservations).Id);
        await Check(h);
        Assert.Empty((await availability.ReadAsync(proposed, h, default)).Reservations);
        proposed.ExpectedStartTime = bufferedEnd;
        await Check(h.AddSeconds(-1));
        Assert.Empty((await availability.ReadAsync(proposed, h.AddSeconds(-1), default)).Reservations);

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reservations SET status = 'PARKING' WHERE id = {id}");
        await Check(h);
        proposed.ExpectedStartTime = start;
        Assert.Equal("VEHICLE_TIME_CONFLICT", (await Assert.ThrowsAsync<ReservationGateException>(() => Check(h))).Code);
        proposed.SiteId = Guid.NewGuid();
        await guards.LockSiteAsync(proposed.SiteId, default);
        await Check(h);
    }
}
