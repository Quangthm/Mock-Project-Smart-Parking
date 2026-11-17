using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.API;
using SmartParking.OperationService.API.Controllers;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationHistoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task AssistantDateRange_FiltersBeforePaginationAndPreservesHistoryLinksAndVehicle()
    {
        var account = Guid.NewGuid();
        var matching = CreateBooking(account);
        matching.VehicleType = "CAR";
        matching.OriginalPlate = "SYNTHETIC";
        var outside = CreateBooking(account);
        outside.CreatedAt = CreatedAt.AddMinutes(5);
        outside.ExpectedStartTime = matching.ExpectedEndTime;
        outside.ExpectedEndTime = matching.ExpectedEndTime.AddHours(1);
        var allocation = CreateAllocation(matching, "RESERVED", CreatedAt);
        await using var db = await CreateDbAsync(matching, outside, allocation);
        await using var services = CreateServices(db, account);
        var result = Assert.Single(await services.GetRequiredService<IMediator>().Send(
            new ReadOwnBookingsQuery(PageSize: 1, From: matching.ExpectedStartTime,
                To: matching.ExpectedEndTime), Ct));
        Assert.Equal(matching.Id, result.ReservationId);
        Assert.Equal(allocation.SlotId, result.AllocatedSlotId);
        Assert.Equal(new BookingVehicle("CAR", "SYNTHETIC"), result.Vehicle);
    }

    [Fact]
    public async Task ListAndDetail_ReturnSameOwnBookingWithRecordedPlateAndIndependentStates()
    {
        var accountId = Guid.NewGuid();
        var booking = CreateBooking(accountId);
        booking.Status = "COMPLETED";
        booking.OriginalPlate = "51A-12345";
        booking.NormalizedPlate = "51A12345";
        booking.RecordOrderCreated(Guid.NewGuid());
        var allocation = CreateAllocation(booking, "RELEASED", CreatedAt);
        var session = CreateSession(booking);
        session.ExitTime = CreatedAt.AddHours(3);
        session.SessionStatus = "COMPLETED";
        allocation.ParkingSessionId = session.Id;

        var foreign = CreateBooking(Guid.NewGuid());
        foreign.OriginalPlate = booking.OriginalPlate;
        var nonBooking = new ReservationRow { Id = Guid.NewGuid(), AccountId = accountId, CreatedAt = CreatedAt };

        await using var db = await CreateDbAsync(booking, foreign, nonBooking, allocation, session);
        await using var services = CreateServices(db, accountId);
        var mediator = services.GetRequiredService<IMediator>();
        var controller = new ReservationBookingController(mediator);
        var item = Assert.Single(await mediator.Send(new ReadOwnBookingsQuery(), Ct));
        var detail = await mediator.Send(new ReadOwnBookingQuery(booking.Id), Ct);

        Assert.Equal(item, detail);
        Assert.Equal(booking.Id, item.ReservationId);
        Assert.Equal(booking.SiteId, item.SiteId);
        Assert.Equal("51A-12345", item.LicensePlate);
        Assert.Equal(booking.CreatedAt, item.CreatedAt);
        Assert.Equal(booking.ExpectedStartTime, item.Start);
        Assert.Equal(booking.ExpectedEndTime, item.End);
        Assert.Equal("COMPLETED", item.State);
        Assert.Equal(booking.TargetSlotId, item.RequestedSlotId);
        Assert.NotEqual(item.RequestedSlotId, item.AllocatedSlotId);
        Assert.Equal(allocation.SlotId, item.AllocatedSlotId);
        Assert.Equal("RELEASED", item.AllocationStatus);
        Assert.Equal(session.Id, item.ParkingSessionId);
        Assert.Equal(booking.PaymentOrderId, item.OrderId);
        Assert.Equal("CREATED", item.OrderProvisioningState);
        Assert.Equal(20_000m, item.BookedAmount);
        Assert.Equal(5_000m, item.DepositAmount);
        Assert.Equal("VND", item.Currency);
        Assert.Null(item.PaymentStatus);
        Assert.Null(item.SiteName);
        Assert.Null(item.SiteAddress);

        using var listJson = ToJson(await controller.List(ct: Ct));
        using var detailJson = ToJson(await controller.Read(booking.Id, Ct));
        Assert.True(listJson.RootElement.GetProperty("success").GetBoolean());
        Assert.True(detailJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Array, listJson.RootElement.GetProperty("data").ValueKind);
        var data = detailJson.RootElement.GetProperty("data");
        Assert.Equal(item.ReservationId, data.GetProperty("reservationId").GetGuid());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("paymentStatus").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("siteName").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("siteAddress").ValueKind);
        Assert.False(data.TryGetProperty("vehicleId", out _));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal("COMPLETED", (await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == booking.Id, Ct)).Status);
    }

    [Fact]
    public async Task ListWithOnlyForeignBookings_IsEmptyAndDetailUsesSameNotFoundAsMissingId()
    {
        var foreign = CreateBooking(Guid.NewGuid());
        await using var db = await CreateDbAsync(foreign);
        await using var services = CreateServices(db, Guid.NewGuid());
        var controller = new ReservationBookingController(services.GetRequiredService<IMediator>());

        using var list = ToJson(await controller.List(ct: Ct));
        Assert.True(list.RootElement.GetProperty("success").GetBoolean());
        Assert.Empty(list.RootElement.GetProperty("data").EnumerateArray());
        foreach (var id in new[] { foreign.Id, Guid.NewGuid() })
        {
            var error = await Assert.ThrowsAsync<ReservationGateException>(() => controller.Read(id, Ct));
            Assert.Equal(404, error.StatusCode);
            Assert.Equal("RESERVATION_NOT_FOUND", error.Code);
        }
    }

    [Fact]
    public async Task ZeroPriceBookingWithNoScopedLinks_ReturnsNullLinksAndPaymentStatus()
    {
        var booking = CreateBooking(Guid.NewGuid(), zeroPrice: true);
        booking.Status = "CONFIRMED";
        booking.NormalizedPlate = "51A12345";

        var otherTenant = CreateAllocation(booking, "OCCUPIED", CreatedAt);
        otherTenant.TenantId = Guid.NewGuid();
        var otherSite = CreateAllocation(booking, "RESERVED", CreatedAt);
        otherSite.SiteId = Guid.NewGuid();
        var foreignSession = CreateSession(booking);
        foreignSession.AccountId = Guid.NewGuid();
        var wrongSiteSession = CreateSession(booking);
        wrongSiteSession.SiteId = Guid.NewGuid();
        var wrongTenantSession = CreateSession(booking);
        wrongTenantSession.TenantId = Guid.NewGuid();

        await using var db = await CreateDbAsync(booking, otherTenant, otherSite, foreignSession, wrongSiteSession, wrongTenantSession);
        await using var services = CreateServices(db, booking.AccountId);

        var detail = await services.GetRequiredService<IMediator>().Send(new ReadOwnBookingQuery(booking.Id), Ct);

        Assert.Equal("CONFIRMED", detail.State);
        Assert.Equal("51A12345", detail.LicensePlate);
        Assert.NotNull(detail.RequestedSlotId);
        Assert.Null(detail.AllocatedSlotId);
        Assert.Null(detail.AllocationStatus);
        Assert.Null(detail.ParkingSessionId);
        Assert.Equal(0m, detail.BookedAmount);
        Assert.Equal(0m, detail.DepositAmount);
        Assert.Null(detail.OrderId);
        Assert.Null(detail.PaymentStatus);
        Assert.Equal("NOT_REQUIRED", detail.OrderProvisioningState);
        Assert.Equal("PENDING", detail.ZeroPriceAcceptanceState);
    }

    [Theory]
    [InlineData("RESERVED")]
    [InlineData("OCCUPIED")]
    public async Task ActiveAllocationAndVisitTakePrecedenceOverLaterHistoricalRows(string activeStatus)
    {
        var booking = CreateBooking(Guid.NewGuid());
        var olderActive = CreateAllocation(booking, activeStatus, CreatedAt.AddMinutes(-1));
        var tiedActive = CreateAllocation(booking, activeStatus, CreatedAt);
        tiedActive.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var active = CreateAllocation(booking, activeStatus, CreatedAt);
        active.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var released = CreateAllocation(booking, "RELEASED", CreatedAt.AddHours(1));
        var visit = CreateSession(booking);
        var laterClosedVisit = CreateSession(booking);
        laterClosedVisit.CreatedAt = CreatedAt.AddHours(1);
        laterClosedVisit.ExitTime = CreatedAt.AddHours(2);

        await using var db = await CreateDbAsync(booking, olderActive, tiedActive, active, released, visit, laterClosedVisit);
        await using var services = CreateServices(db, booking.AccountId);

        var item = Assert.Single(await services.GetRequiredService<IMediator>().Send(new ReadOwnBookingsQuery(), Ct));

        Assert.Equal(active.SlotId, item.AllocatedSlotId);
        Assert.Equal(activeStatus, item.AllocationStatus);
        Assert.Equal(visit.Id, item.ParkingSessionId);
    }

    [Fact]
    public async Task PaginationUsesCreationTimeThenIdAndValidatesCursorPair()
    {
        var accountId = Guid.NewGuid();
        var low = CreateBooking(accountId);
        low.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var high = CreateBooking(accountId);
        high.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var newer = CreateBooking(accountId);
        newer.CreatedAt = CreatedAt.AddMinutes(1);
        var older = CreateBooking(accountId);
        older.CreatedAt = CreatedAt.AddMinutes(-1);
        await using var db = await CreateDbAsync(low, high, newer, older);
        await using var services = CreateServices(db, accountId);
        var mediator = services.GetRequiredService<IMediator>();

        var first = await mediator.Send(new ReadOwnBookingsQuery(PageSize: 2), Ct);
        Assert.Equal(new[] { newer.Id, high.Id }, first.Select(x => x.ReservationId));
        var cursor = first[^1];
        var second = await mediator.Send(new ReadOwnBookingsQuery(2, cursor.CreatedAt, cursor.ReservationId), Ct);
        Assert.Equal(new[] { low.Id, older.Id }, second.Select(x => x.ReservationId));
        Assert.Empty(await mediator.Send(new ReadOwnBookingsQuery(2, older.CreatedAt, older.Id), Ct));

        await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.Send(new ReadOwnBookingsQuery(Before: CreatedAt), Ct));
        await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.Send(new ReadOwnBookingsQuery(Before: CreatedAt, BeforeId: Guid.Empty), Ct));
        await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.Send(new ReadOwnBookingsQuery(PageSize: 101), Ct));
    }

    [Theory]
    [InlineData("operator", true)]
    [InlineData("driver", false)]
    public async Task DriverRoleAndAccountClaimsAreRequired(string role, bool includeAccount)
    {
        await using var db = await CreateDbAsync();
        await using var services = CreateServices(db, includeAccount ? Guid.NewGuid() : null, role);
        var mediator = services.GetRequiredService<IMediator>();

        var list = await Assert.ThrowsAsync<ReservationGateException>(() => mediator.Send(new ReadOwnBookingsQuery(), Ct));
        var detail = await Assert.ThrowsAsync<ReservationGateException>(() => mediator.Send(new ReadOwnBookingQuery(Guid.NewGuid()), Ct));
        Assert.Equal(403, list.StatusCode);
        Assert.Equal(403, detail.StatusCode);
    }

    [Fact]
    public void OriginalBookingViewConstructorAndJsonFieldsRemainCompatible()
    {
        var booking = new BookingView(Guid.NewGuid(), "CONFIRMED", Guid.NewGuid(), null, CreatedAt,
            CreatedAt.AddHours(1), null, null, "NOT_REQUIRED", null, "ACCEPTED", 0, 0, "VND", CreatedAt, CreatedAt);
        string[] originalNames =
        [
            "reservationId", "state", "siteId", "requestedSlotId", "start", "end", "holdExpiresAt", "orderId",
            "orderProvisioningState", "orderProvisioningCode", "zeroPriceAcceptanceState", "bookedAmount",
            "depositAmount", "currency", "confirmedAt", "createdAt"
        ];
        var json = JsonSerializer.SerializeToElement(booking, JsonOptions);

        Assert.Equal(originalNames, json.EnumerateObject().Take(originalNames.Length).Select(p => p.Name));
        foreach (var name in new[] { "siteName", "siteAddress", "licensePlate", "allocatedSlotId", "allocationStatus", "parkingSessionId", "paymentStatus" })
            Assert.Equal(JsonValueKind.Null, json.GetProperty(name).ValueKind);
    }

    private static JsonDocument ToJson(IActionResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value, JsonOptions));

    private static async Task<ReservationDbContext> CreateDbAsync(params object[] rows)
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ReservationDbContext(options);
        db.AddRange(rows);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        return db;
    }

    private static ServiceProvider CreateServices(ReservationDbContext db, Guid? accountId, string role = "driver")
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()), new("role", role) };
        if (accountId is { } account)
            claims.Add(new Claim("account_id", account.ToString()));

        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddSingleton<ICurrentActor>(new HttpCurrentActor(http));
        services.AddSingleton<IReservationBookingRepository>(new ReservationRepository(db));
        return services.BuildServiceProvider();
    }

    private static ReservationRow CreateBooking(Guid accountId, bool zeroPrice = false) =>
        ReservationRow.Prepare(Guid.NewGuid(), Guid.NewGuid(), accountId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "terms-v1", "PAYOS", CreatedAt.AddHours(1),
            CreatedAt.AddHours(2), CreatedAt.AddHours(2), CreatedAt, CreatedAt.AddMinutes(5),
            zeroPrice ? 0m : 20_000m, zeroPrice ? 0m : 5_000m, "VND", "{}", "{}");

    private static SlotAllocationRow CreateAllocation(ReservationRow booking, string status, DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(), ReservationId = booking.Id, TenantId = booking.TenantId, SiteId = booking.SiteId,
            SlotId = Guid.NewGuid(), AllocationStatus = status, CreatedAt = createdAt,
            AllocatedStartTime = booking.ExpectedStartTime, AllocatedEndTime = booking.ExpectedEndTime
        };

    private static ParkingSessionRow CreateSession(ReservationRow booking) =>
        new()
        {
            Id = Guid.NewGuid(), ReservationId = booking.Id, TenantId = booking.TenantId, SiteId = booking.SiteId,
            AccountId = booking.AccountId, EntryTime = booking.ExpectedStartTime, SessionStatus = "ACTIVE",
            CreatedAt = booking.ExpectedStartTime
        };
}
