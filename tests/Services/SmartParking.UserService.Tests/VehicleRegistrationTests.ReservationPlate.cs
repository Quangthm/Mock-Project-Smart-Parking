using Microsoft.EntityFrameworkCore;
using UserService.Application.Authorization;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed partial class VehicleRegistrationTests
{
    [PostgresFact]
    public async Task AdmissionPlateUsesCurrentOwnerAndNeverReturnsForeignRegistrationEvidence()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var first = Driver("first@example.test");
        var second = Driver("second@example.test");
        await using var db = fixture.Open();
        db.Users.AddRange(first, second);
        await db.SaveChangesAsync();
        var firstId = first.Accounts.Single().Id!.Value;
        var secondId = second.Accounts.Single().Id!.Value;
        var resolver = new ResolveEligibleRegisteredPlateHandler(new VehicleRepository(db));
        ResolveEligibleRegisteredPlate Check(Guid owner) => new(new(Guid.NewGuid(), Guid.NewGuid(), owner, "binding", "51A12345"));
        Assert.False((await resolver.Handle(Check(firstId), default)).Eligible);
        await (await Service(db, first)).SaveAsync(first.Id!.Value, null, Vehicle(), default);
        var accepted = await resolver.Handle(Check(firstId), default);
        Assert.True(accepted.Eligible);
        Assert.Equal("CAR", accepted.VehicleType);
        Assert.True(accepted.Revision > 0);
        var foreign = await resolver.Handle(Check(secondId), default);
        Assert.False(foreign.Eligible);
        Assert.Null(foreign.Revision);
        var registered = await db.RegisteredVehicles.SingleAsync();
        registered.AccountId = secondId;
        var work = new UnitOfWork(db, new UserRepository(db));
        await work.SaveChangesAsync();
        Assert.False((await resolver.Handle(Check(firstId), default)).Eligible);
        Assert.True((await resolver.Handle(Check(secondId), default)).Eligible);
        registered.DeletedAt = DateTimeOffset.UtcNow;
        await work.SaveChangesAsync();
        Assert.False((await resolver.Handle(Check(secondId), default)).Eligible);
    }
}
