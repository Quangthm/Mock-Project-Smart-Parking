using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Usecases.Gate.Commands.ClaimGateSlot;
using SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseGateSlot;
using SmartParking.FacilityService.Persistence;

namespace SmartParking.FacilityService.Tests;

// Exercise the real handler-owned transaction; repositories cannot commit independently.
internal sealed class GateCommandFixture(NpgsqlDataSource source)
{
    public async Task<OperatorGateSlot> ClaimAvailableAsync(Guid tenant, Guid site, string category,
        Guid? slot, Guid? unit, bool reserved, CancellationToken ct)
    {
        await using var db = Context();
        var handler = new ClaimGateSlotCommandHandler(new OperatorGateSlotRepository(db), new UnitOfWork(db));
        return await handler.Handle(new(tenant, site, category, slot, unit, reserved), ct);
    }

    public async Task ReleaseAsync(Guid tenant, Guid site, Guid slot, CancellationToken ct)
    {
        await using var db = Context();
        var handler = new ReleaseGateSlotCommandHandler(new OperatorGateSlotRepository(db), new UnitOfWork(db));
        await handler.Handle(new(tenant, site, slot), ct);
    }

    private ParkingDbContext Context() => new(new DbContextOptionsBuilder<ParkingDbContext>()
        .UseNpgsql(source).Options);
}
