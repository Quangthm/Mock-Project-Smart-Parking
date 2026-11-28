using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests;

public sealed class SlotAllocationRepositoryTests
{
    [Fact]
    public async Task VisitLookupIncludesAllOwnedActiveRowsAndExcludesOtherOwners()
    {
        using var db = new ReservationDbContext(new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid();
        var site = Guid.NewGuid();
        var session = Guid.NewGuid();
        var reservation = Guid.NewGuid();
        SlotAllocationRow Row(Guid? owner, Guid? visit, string status = "RESERVED") => new()
        {
            Id = Guid.NewGuid(), TenantId = tenant, SiteId = site, SlotId = Guid.NewGuid(),
            ReservationId = owner, ParkingSessionId = visit, AllocationStatus = status
        };
        var first = Row(reservation, null);
        var second = Row(reservation, null);
        var occupied = Row(reservation, session, "OCCUPIED");
        var walkIn = Row(null, Guid.NewGuid(), "OCCUPIED");
        var otherSite = Row(reservation, null);
        otherSite.SiteId = Guid.NewGuid();
        var otherTenant = Row(reservation, null);
        otherTenant.TenantId = Guid.NewGuid();
        db.SlotAllocations.AddRange(first, second, occupied, walkIn, otherSite, otherTenant,
            Row(Guid.NewGuid(), null), Row(reservation, Guid.NewGuid()), Row(Guid.NewGuid(), session),
            Row(reservation, null, "RELEASED"), Row(null, null));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new SlotAllocationRepository(db);
        var rows = await repository.FindActiveForVisitAsync(tenant, site, session, reservation, default);
        Assert.Equal(new[] { first.Id, second.Id, occupied.Id }.Order(), rows.Select(row => row.Id).Order());
        var walkInRows = await repository.FindActiveForVisitAsync(tenant, site, walkIn.ParkingSessionId!.Value, null, default);
        Assert.Equal(walkIn.Id, Assert.Single(walkInRows).Id);
    }
}
