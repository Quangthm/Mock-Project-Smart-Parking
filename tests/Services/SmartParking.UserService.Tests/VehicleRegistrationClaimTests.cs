using Microsoft.EntityFrameworkCore.Diagnostics;
using UserService.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Models.Exceptions;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed partial class VehicleRegistrationTests
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task ConcurrentClaimantsPreserveWinnerAndReplayNeverPromotesAfterRemoval()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var a = Driver("race-a@example.test"); var b = Driver("race-b@example.test");
        await using (var db = fixture.Open()) { db.Users.AddRange(a, b); await db.SaveChangesAsync(); }
        async Task<string> Register(User user, string plate)
        {
            await using var db = fixture.Open();
            try { await (await Service(db, user)).SaveAsync(user.Id!.Value, null, Vehicle(plate), default); return "ACTIVE"; }
            catch (AuthException e) { return e.Code; }
        }
        var outcomes = await Task.WhenAll(Register(a, " 51a-123.45 "), Register(b, "51A12345"));
        Assert.Single(outcomes, o => o == "ACTIVE");
        Assert.Single(outcomes, o => o == "PLATE_EXISTS");
        VehicleRegistrationClaim original;
        User loser = outcomes[0] == "ACTIVE" ? b : a;
        await using (var db = fixture.Open())
        {
            var incumbent = await db.RegisteredVehicles.SingleAsync();
            original = await db.VehicleRegistrationClaims.AsNoTracking().SingleAsync();
            Assert.NotEqual(incumbent.AccountId, original.AccountId);
            var asset = await db.MediaAssets.SingleAsync(m => m.Id == original.ImageAssetId);
            Assert.Equal(original.Id, asset.RegistrationClaimId);
            Assert.Null(asset.VehicleId);
            Assert.Equal(asset.AdmittedAt.AddDays(30), asset.RetainUntil);
            Assert.NotNull(await new VehicleRepository(db).ResolveBindingAsync(incumbent.AccountId!.Value, incumbent.Id, default));
            Assert.Null(await new VehicleRepository(db).ResolveBindingAsync(loser.Accounts.Single().Id!.Value, original.Id, default));
            (await db.RegisteredVehicles.SingleAsync()).DeletedAt = DateTimeOffset.UtcNow;
            await new UnitOfWork(db, new UserRepository(db)).SaveChangesAsync();
        }
        Assert.Equal("PLATE_EXISTS", await Register(loser, "51-A-12345"));
        await using (var db = fixture.Open())
        {
            var replay = await db.VehicleRegistrationClaims.SingleAsync();
            Assert.Equal(original.Id, replay.Id);
            Assert.Equal(original.ImageAssetId, replay.ImageAssetId);
            Assert.Equal(original.CreatedAt, replay.CreatedAt);
            Assert.Empty(await db.RegisteredVehicles.Where(v => v.DeletedAt == null).ToArrayAsync());
        }
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task SharedKeyResolverRejectsAnonymousAndPreservesRealCategoryAndRevision()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var driver = Driver("resolver@example.test");
        Guid vehicleId;
        await using (var db = fixture.Open())
        {
            db.Users.Add(driver); await db.SaveChangesAsync();
            vehicleId = (await (await Service(db, driver)).SaveAsync(driver.Id!.Value, null, Vehicle("59-x1 123.45", "MOTORCYCLE"), default)).Id;
        }
        await using var app = await fixture.StartHostAsync();
        using var client = app.GetTestClient();
        var body = new { accountId = driver.Accounts.Single().Id, vehicleId };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/internal/vehicle-bindings/resolve", body)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Service-Key", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/internal/vehicle-bindings/resolve", body)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Service-Key");
        client.DefaultRequestHeaders.Add("X-Service-Key", "isolated-user-service-tests-key-20261012");
        var response = await client.PostAsJsonAsync("/internal/vehicle-bindings/resolve", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("ACTIVE", result.GetProperty("state").GetString());
        Assert.Equal("MOTORCYCLE", result.GetProperty("vehicleType").GetString());
        Assert.False(result.TryGetProperty("rawPlate", out _));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/internal/vehicle-bindings/resolve", new { body.accountId, vehicleId, expectedRevision = 999 })).StatusCode);
        var absent = await client.PostAsJsonAsync("/internal/vehicle-bindings/resolve", new { accountId = Guid.NewGuid(), vehicleId });
        Assert.Equal("INELIGIBLE", (await absent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("state").GetString());
        await using (var db = fixture.Open())
        {
            (await db.RegisteredVehicles.SingleAsync(v => v.Id == vehicleId)).DeletedAt = DateTimeOffset.UtcNow;
            await new UnitOfWork(db, new UserRepository(db)).SaveChangesAsync();
        }
        var removed = await client.PostAsJsonAsync("/internal/vehicle-bindings/resolve", body);
        Assert.Equal("INELIGIBLE", (await removed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("state").GetString());
        await using (var db = fixture.Open())
            Assert.Equal(2L, await db.RegisteredVehicles.Where(v => v.Id == vehicleId).Select(v => EF.Property<long>(v, "BindingRevision")).SingleAsync());
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task CanonicalSchemaIsRerunnableAndKeepsVehicleIdentity()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        await using var db = fixture.Open();
        var driver = Driver("upgrade@example.test"); db.Users.Add(driver); await db.SaveChangesAsync();
        var vehicle = await (await Service(db, driver)).SaveAsync(driver.Id!.Value, null, Vehicle(), default);
        var upgrade = await File.ReadAllTextAsync(ServiceSchema.ScriptPath("microservices/01-user-access-service-db.sql"));
        await db.Database.ExecuteSqlRawAsync(upgrade);
        await db.Database.ExecuteSqlRawAsync(upgrade);
        Assert.Equal(vehicle.Id, (await db.RegisteredVehicles.SingleAsync()).Id);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task SameAccountConcurrentReplayCreatesNoClaimAndNullOwnerStaysProtected()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var driver = Driver("same-account-race@example.test");
        await using (var db = fixture.Open())
        {
            db.Users.Add(driver); await db.SaveChangesAsync();
            // Prepare the shared authentication session before the concurrent writes.
            _ = await Service(db, driver);
        }
        async Task<string> Save()
        {
            await using var db = fixture.Open();
            try { await (await Service(db, driver)).SaveAsync(driver.Id!.Value, null, Vehicle(), default); return "ACTIVE"; }
            catch (AuthException e) { return e.Code; }
        }
        var outcomes = await Task.WhenAll(Save(), Save());
        Assert.Single(outcomes, o => o == "ACTIVE");
        Assert.Single(outcomes, o => o == "PLATE_EXISTS");
        await using (var db = fixture.Open())
        {
            Assert.Single(await db.RegisteredVehicles.ToArrayAsync());
            Assert.Empty(await db.VehicleRegistrationClaims.ToArrayAsync());
            await db.RegisteredVehicles.ExecuteUpdateAsync(u => u.SetProperty(v => v.AccountId, (Guid?)null));
        }
        Assert.Equal("PLATE_EXISTS", await Save());
        await using (var db = fixture.Open())
        {
            Assert.Null((await db.RegisteredVehicles.SingleAsync()).AccountId);
            Assert.Single(await db.VehicleRegistrationClaims.ToArrayAsync());
        }
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task FailureAfterClaimInsertRollsBackClaimAndPhotoAttachment()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var a = Driver("rollback-a@example.test"); var b = Driver("rollback-b@example.test");
        await using (var db = fixture.Open())
        {
            db.Users.AddRange(a, b); await db.SaveChangesAsync();
            await (await Service(db, a)).SaveAsync(a.Id!.Value, null, Vehicle(), default);
        }
        var body = Vehicle();
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString).AddInterceptors(new FailClaimAttachment()).Options))
            await Assert.ThrowsAsync<DbUpdateException>(async () => await (await Service(db, b)).SaveAsync(b.Id!.Value, null, body, default));
        await using (var db = fixture.Open())
        {
            Assert.Empty(await db.VehicleRegistrationClaims.ToArrayAsync());
            Assert.Null((await db.MediaAssets.SingleAsync(a => a.Id == body.ImageAssetId)).RegistrationClaimId);
            Assert.Equal(a.Accounts.Single().Id, (await db.RegisteredVehicles.SingleAsync()).AccountId);
        }
    }

    private sealed class FailClaimAttachment : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<MediaAsset>().Any(e =>
                e.State == EntityState.Modified && e.Entity.RegistrationClaimId is not null))
                throw new DbUpdateException("Synthetic failure after the claim insert and before attachment save.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
