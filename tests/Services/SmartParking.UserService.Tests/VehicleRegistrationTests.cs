using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;
using UserService.Persistence.Repositories;
using UserService.Persistence;
using UserService.Application.Services;

namespace SmartParking.UserService.Tests;

public sealed class VehicleRegistrationTests
{
    [Fact]
    public void DomainNormalizerRejectsMissingInputWithoutTransportOrDatabaseDependencies()
    {
        Assert.False(SmartParking.UserService.Domain.ValueObjects.LicensePlateNormalizer.TryNormalize(null, "CAR", out var value));
        Assert.Equal("", value);
    }
    [Theory]
    [InlineData("51a-123.45", "CAR", "51A12345")]
    [InlineData(" 51 A 12345 ", "CAR", "51A12345")]
    [InlineData("51A12345", "CAR", "51A12345")]
    [InlineData("59-x1 123.45", "MOTORCYCLE", "59X112345")]
    public void EquivalentFormatsHaveStableCanonicalValue(string raw, string type, string expected)
    {
        var canonical = VehicleRegistrationService.NormalizePlate(raw, type);
        Assert.Equal(expected, canonical);
        Assert.Equal(canonical, VehicleRegistrationService.NormalizePlate(canonical, type));
    }

    [Theory]
    [InlineData("", "CAR")]
    [InlineData("51A1234", "CAR")]
    [InlineData("51A123456", "CAR")]
    [InlineData("51A12/345", "CAR")]
    [InlineData("59X112345", "CAR")]
    [InlineData("51A12345", "MOTORCYCLE")]
    [InlineData("51A12345", "BICYCLE")]
    [InlineData("NO PLATE", "CAR")]
    public void InvalidApplicationFormatsAndCategoriesAreRejected(string raw, string type)
    {
        Assert.Equal("INVALID_PLATE", Assert.Throws<AuthException>(() => VehicleRegistrationService.NormalizePlate(raw, type)).Code);
    }

    private static async Task<VehicleRegistrationService> Service(AppDbContext db, User driver) =>
        new(new VehicleRepository(db), new UnitOfWork(db, new UserRepository(db)), TimeProvider.System,
            await TestAuthorization.ForUserAsync(db, TimeProvider.System, driver.Id!.Value,
                accountId: driver.Accounts.First(a => a.RoleCode == "DRIVER").Id));

    private static User Driver(string email) => new()
    {
        Id = Guid.NewGuid(), FullName = "Driver", Email = email, Status = UserStatus.Active,
        PasswordHash = new BcryptPasswordService().Hash("Password@123"),
        Accounts = [new Account { Id = Guid.NewGuid(), RoleCode = "DRIVER" }]
    };
    private static VehicleDto Vehicle(string plate = " 51a-123.45 ", string type = "CAR") => new()
    { Plate = plate, VehicleType = type, ImageReference = "https://images.example.test/vehicle.jpg" };

    [PostgresFact]
    public async Task RejectedMutationsDoNotPersistAndServiceRechecksCurrentDriver()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var driver = Driver("state@example.test"); Guid id;
        await using (var db = fixture.Open())
        {
            db.Users.Add(driver); await db.SaveChangesAsync();
            id = (await (await Service(db, driver)).SaveAsync(driver.Id!.Value, null, Vehicle(), default)).Id;
        }
        foreach (var invalid in new[] { Vehicle("invalid"), Vehicle("51A12345", "BICYCLE"),
            new VehicleDto { Plate = "51A12345", VehicleType = "CAR" },
            new VehicleDto { Plate = "51A12345", VehicleType = "CAR", ImageReference = "http://images.example.test/car.jpg" } })
        {
            await using var db = fixture.Open();
            var service = await Service(db, driver);
            await Assert.ThrowsAsync<AuthException>(() => service.SaveAsync(driver.Id!.Value, id, invalid, default));
        }
        foreach (var state in new[] { UserStatus.PendingVerification, UserStatus.Locked, UserStatus.Inactive })
        {
            await using (var db = fixture.Open()) { await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, state)); }
            await using var check = fixture.Open();
            var service = await Service(check, driver);
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => service.ListAsync(driver.Id!.Value, default))).Code);
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => service.SaveAsync(driver.Id!.Value, id, Vehicle("51B54321"), default))).Code);
        }
        await using (var db = fixture.Open())
        {
            await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Active));
            await db.Accounts.ExecuteUpdateAsync(s => s.SetProperty(a => a.RoleCode, "OWNER"));
            var service = await Service(db, driver);
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => service.ListAsync(driver.Id!.Value, default))).Code);
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => service.SaveAsync(driver.Id!.Value, null, Vehicle(), default))).Code);
        }
        await using (var db = fixture.Open())
        {
            var stored = await db.RegisteredVehicles.SingleAsync();
            Assert.Equal(" 51a-123.45 ", stored.RawPlate); Assert.Equal("51A12345", stored.CanonicalPlate);
            Assert.Equal(driver.Accounts.Single().Id, stored.AccountId);
        }
    }

    [PostgresFact]
    public async Task PersistenceOwnershipUpdatesAndOpenDuplicatePolicy()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var a = Driver("a@example.test"); var b = Driver("b@example.test");
        await using (var db = fixture.Open()) { db.Users.AddRange(a, b); await db.SaveChangesAsync(); }
        Guid first;
        await using (var db = fixture.Open())
        {
            var service = await Service(db, a);
            first = (await service.SaveAsync(a.Id!.Value, null, Vehicle(), default)).Id;
            await service.SaveAsync(a.Id.Value, null, Vehicle("59-x1 123.45", "MOTORCYCLE"), default);
            var otherService = await Service(db, b);
            Assert.Empty(await otherService.ListAsync(b.Id!.Value, default));
            Assert.Equal("VEHICLE_NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => otherService.SaveAsync(b.Id.Value, first, Vehicle(), default))).Code);
            // FR-VEH-02 remains OPEN: duplicate canonical values grant no access to another account.
            await otherService.SaveAsync(b.Id.Value, null, Vehicle("51A12345"), default);
            Assert.Single(await otherService.ListAsync(b.Id.Value, default));
        }
        await using (var db = fixture.Open())
        {
            var service = await Service(db, a);
            var list = await service.ListAsync(a.Id!.Value, default);
            Assert.Equal(2, list.Count);
            var stored = Assert.Single(list, v => v.Id == first);
            Assert.Equal(" 51a-123.45 ", stored.RawPlate); Assert.Equal("51A12345", stored.CanonicalPlate);
            Assert.Equal(a.Accounts.Single().Id, stored.AccountId);
            Assert.Equal(Vehicle().ImageReference, stored.ImageReference);
            var edit = Vehicle("59x2-543.21", "MOTORCYCLE");
            edit.ImageReference = "https://images.example.test/updated-motorcycle.jpg";
            await service.SaveAsync(a.Id.Value, first, edit, default);
        }
        await using (var db = fixture.Open())
        {
            var updated = await db.RegisteredVehicles.SingleAsync(v => v.Id == first);
            Assert.Equal("59x2-543.21", updated.RawPlate); Assert.Equal("59X254321", updated.CanonicalPlate);
            Assert.Equal("MOTORCYCLE", updated.VehicleType);
            Assert.Equal("https://images.example.test/updated-motorcycle.jpg", updated.ImageReference);
            Assert.Equal(a.Accounts.Single().Id, updated.AccountId);
            await db.RegisteredVehicles.Where(v => v.Id == first).ExecuteUpdateAsync(s => s.SetProperty(v => v.DeletedAt, DateTimeOffset.UtcNow));
            var service = await Service(db, a);
            Assert.Single(await service.ListAsync(a.Id!.Value, default));
            Assert.Equal("VEHICLE_NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => service.SaveAsync(a.Id.Value, first, Vehicle(), default))).Code);
            Assert.Equal(3, await db.RegisteredVehicles.CountAsync());
        }
    }

    [PostgresFact]
    public async Task RealHttpValidationCrossAccountAndCurrentAccountState()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var a = Driver("http-a@example.test"); var b = Driver("http-b@example.test");
        await using (var db = fixture.Open()) { db.Users.AddRange(a, b); await db.SaveChangesAsync(); }
        await using var app = await fixture.StartHostAsync();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/vehicles")).StatusCode);
        async Task Login(string email)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password@123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            client.DefaultRequestHeaders.Authorization = new("Bearer", data.GetProperty("accessToken").GetString());
        }
        await Login(a.Email!);
        var created = await client.PostAsJsonAsync("/api/vehicles", Vehicle());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var vehicle = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var id = vehicle.GetProperty("id").GetGuid();
        Assert.Equal("51A12345", vehicle.GetProperty("canonicalPlate").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/vehicles/{id}", Vehicle("51b-543.21"))).StatusCode);
        foreach (var invalid in new object[]
        {
            Vehicle("invalid"), Vehicle("51A12345", "BICYCLE"),
            new { plate = "51A12345", vehicleType = "CAR" },
            new { plate = "51A12345", vehicleType = "CAR", imageReference = "http://images.example.test/car.jpg" },
            new { plate = "51A12345", vehicleType = "CAR", imageReference = "https://images.example.test/car.jpg", accountId = b.Accounts.Single().Id }
        }) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/vehicles", invalid)).StatusCode);
        await Login(b.Email!);
        var list = (await (await client.GetAsync("/api/vehicles")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(0, list.GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync($"/api/vehicles/{id}", Vehicle())).StatusCode);
        await Login(a.Email!);
        await using (var db = fixture.Open())
        {
            await db.Accounts.Where(x => x.UserId == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "LOCKED"));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/vehicles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync($"/api/vehicles/{id}", Vehicle())).StatusCode);
        await using (var db = fixture.Open())
        {
            await db.Accounts.Where(x => x.UserId == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "ACTIVE"));
            await db.Users.Where(x => x.Id == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, UserStatus.Inactive));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/vehicles", Vehicle())).StatusCode);
        await using (var db = fixture.Open())
        {
            Assert.Single(await db.RegisteredVehicles.ToArrayAsync());
            var stored = await db.RegisteredVehicles.SingleAsync();
            Assert.Equal(a.Accounts.Single().Id, stored.AccountId);
            Assert.Equal("51b-543.21", stored.RawPlate); Assert.Equal("51B54321", stored.CanonicalPlate);
        }
    }

    [PostgresFact]
    public async Task CurrentDriverAccountScopeExcludesDeletedAndNonPlatedVehicles()
    {
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var driver = Driver("scope@example.test");
        var driverAccount = driver.Accounts.Single();
        var otherAccount = new Account { Id = Guid.NewGuid(), RoleCode = "BUSINESS_OWNER" };
        driver.Accounts.Add(otherAccount);
        var emptyPlateId = Guid.NewGuid();
        await using (var db = fixture.Open())
        {
            db.Users.Add(driver); await db.SaveChangesAsync();
            db.RegisteredVehicles.AddRange(
                new RegisteredVehicle { Id = emptyPlateId, AccountId = driverAccount.Id, RawPlate = "", CanonicalPlate = "", VehicleType = "CAR", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
                new RegisteredVehicle { Id = Guid.NewGuid(), AccountId = otherAccount.Id, RawPlate = "51B54321", CanonicalPlate = "51B54321", VehicleType = "CAR", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            var service = await Service(db, driver);
            Assert.Empty(await service.ListAsync(driver.Id!.Value, default));
            Assert.Equal("VEHICLE_NOT_FOUND", (await Assert.ThrowsAsync<AuthException>(() => service.SaveAsync(driver.Id.Value, emptyPlateId, Vehicle(), default))).Code);
            var saved = await service.SaveAsync(driver.Id.Value, null, Vehicle(), default);
            Assert.Equal(driverAccount.Id, saved.AccountId);
        }
        foreach (var state in new[] { "LOCKED", "INACTIVE", "PENDING_APPROVAL" })
        {
            await using var db = fixture.Open();
            await db.Accounts.Where(a => a.Id == driverAccount.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, state));
            var service = await Service(db, driver);
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => service.ListAsync(driver.Id!.Value, default))).Code);
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => service.SaveAsync(driver.Id!.Value, null, Vehicle(), default))).Code);
        }
        await using (var db = fixture.Open())
        {
            await db.Accounts.Where(a => a.Id == driverAccount.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, "ACTIVE").SetProperty(a => a.DeletedOn, DateTimeOffset.UtcNow));
            var service = await Service(db, driver);
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => service.ListAsync(driver.Id!.Value, default))).Code);
            await db.Accounts.Where(a => a.Id == driverAccount.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.DeletedOn, (DateTimeOffset?)null));
            await db.Users.Where(u => u.Id == driver.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletedOn, DateTimeOffset.UtcNow));
            Assert.Equal("FORBIDDEN", (await Assert.ThrowsAsync<AuthException>(() => service.SaveAsync(driver.Id!.Value, null, Vehicle(), default))).Code);
        }
    }
}
