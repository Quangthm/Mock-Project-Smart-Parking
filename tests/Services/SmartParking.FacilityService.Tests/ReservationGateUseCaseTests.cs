using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Usecase.ReservationGate;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

using OperationsUnitOfWork = SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWork;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationGateUseCaseTests
{
    [Fact]
    public async Task MediatorRejectsInvalidInputBeforeIdentityOrPersistence()
    {
        var data = new GateTestRepositories();
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(new CheckInWalkInCommand(data.Site, "!", "http://invalid", "HELICOPTER")));
        Assert.Empty(data.Calls);
    }

    [Fact]
    public async Task WalkInAcquiresGuardBeforeReadAndCommitsOnce()
    {
        var data = new GateTestRepositories();
        using var services = CreateServices(data);
        var result = await services.GetRequiredService<IMediator>().Send(new CheckInWalkInCommand(data.Site, "29A-12345", null, "CAR"));
        Assert.Equal("29A12345", data.Session!.NormalizedPlate);
        Assert.Equal(data.Session.Id, result.Id);
        Assert.Null(result.SlotId);
        Assert.Null(data.Session.LocationScannedAt);
        Assert.Null(data.Allocation);
        Assert.Equal(new[] { "identity", "begin", "plate-guard", "guard", "active-read", "add-session", "audit", "save", "commit" }, data.Calls);
    }

    [Fact]
    public async Task ExplicitWalkInCheckInRecordsAndReleasesOnlyTheSessionSlot()
    {
        var data = new GateTestRepositories();
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        var slotId = Guid.NewGuid();

        var result = await mediator.Send(new CheckInWalkInCommand(data.Site, "29A12345", null, "CAR", slotId));

        Assert.Equal(slotId, result.SlotId);
        Assert.NotNull(data.Session!.LocationScannedAt);
        Assert.Null(data.Allocation);
        Assert.Equal(new[] { "identity", "begin", "plate-guard", "guard", "active-read", "availability", "claim-slot",
            "add-session", "audit", "save", "commit" }, data.Calls);

        var departure = await mediator.Send(new CheckOutWalkInCommand(data.Site, result.Id, "29A12345", null));
        Assert.NotNull(departure.ExitTime);
        Assert.Equal(slotId, data.ReleasedSlotId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhysicalOccupancyRejectsAConflictingReservationBeforeFacilityClaim(bool scan)
    {
        var data = new GateTestRepositories();
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        var slotId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        data.Reservation = new()
        {
            Id = Guid.NewGuid(), Status = "CONFIRMED", VehicleType = "CAR", TargetSlotId = slotId,
            ExpectedStartTime = now.AddMinutes(-5), ExpectedEndTime = now.AddMinutes(5)
        };

        var error = await Assert.ThrowsAsync<ReservationGateException>(async () =>
        {
            if (scan)
            {
                var walkIn = await mediator.Send(new CheckInWalkInCommand(data.Site, "29A12345", null, "CAR"));
                await mediator.Send(new ScanParkingSessionCommand(data.Site, walkIn.Id, slotId));
            }
            else
            {
                await mediator.Send(new CheckInWalkInCommand(data.Site, "29A12345", null, "CAR", slotId));
            }
        });
        Assert.Equal("SLOT_TIME_CONFLICT", error.Code);
        Assert.DoesNotContain("claim-slot", data.Calls);
        Assert.Null(data.Session?.CurrentSlotId);
        Assert.Null(data.Allocation);
    }

    [Fact]
    public async Task UnavailableFacilitySlotLeavesExplicitCheckInUncommitted()
    {
        var data = new GateTestRepositories { SlotUnavailable = true };
        using var services = CreateServices(data);
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => services.GetRequiredService<IMediator>()
            .Send(new CheckInWalkInCommand(data.Site, "29A12345", null, "CAR", Guid.NewGuid())));
        Assert.Equal("NO_AVAILABLE_SLOT", error.Code);
        Assert.Null(data.Session);
        Assert.DoesNotContain("commit", data.Calls);
    }

    [Fact]
    public async Task DuplicateVehicleDoesNotWriteOrCommit()
    {
        var data = new GateTestRepositories { AlreadyInside = true };
        using var services = CreateServices(data);
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => services.GetRequiredService<IMediator>()
            .Send(new CheckInWalkInCommand(data.Site, "29A-12345", null, "CAR")));
        Assert.Equal("VEHICLE_ALREADY_INSIDE", error.Code);
        Assert.DoesNotContain("save", data.Calls);
        Assert.DoesNotContain("commit", data.Calls);
    }

    [Theory]
    [InlineData("CONFIRMED", null, 0)]
    [InlineData("CONFIRMED", "RESERVED", 0)]
    [InlineData("ALLOCATED", "RESERVED", 0)]
    [InlineData("CONFIRMED", "RESERVED", 2)]
    [InlineData("CONFIRMED", "RESERVED", -2)]
    [InlineData("CONFIRMED", "RELEASED", 0)]
    public async Task ReservationArrivalRecordsOnlyGateEvidence(string state, string? allocationStatus, int offsetHours)
    {
        var data = ReservationFixture(allocationStatus, offsetHours);
        var reservation = data.Reservation!;
        reservation.Status = state;
        using var services = CreateServices(data);
        const string image = "data:image/png;base64,AQ==";
        var result = await services.GetRequiredService<IMediator>().Send(
            new CheckInReservationCommand(data.Site, reservation.Id, "29A12345", image));

        Assert.Equal(reservation.Id, result.Id);
        Assert.Equal("PARKING", reservation.Status);
        var session = Assert.IsType<ParkingSessionRow>(data.Session);
        Assert.Equal("ACTIVE", session.SessionStatus);
        Assert.Equal(reservation.Id, session.ReservationId);
        Assert.Equal(image, session.EntryImageUrl);
        Assert.Equal(session.EntryTime, Assert.Single(data.Events).OccurredAt);
        Assert.Null(session.CurrentSlotId);
        Assert.Null(session.LocationScannedAt);
        Assert.Null(data.Allocation?.ParkingSessionId);
        Assert.Equal(allocationStatus, data.Allocation?.AllocationStatus);
        Assert.DoesNotContain("read-allocations", data.Calls);
        Assert.DoesNotContain("claim-slot", data.Calls);
        Assert.Contains("commit", data.Calls);
    }

    [Theory]
    [InlineData("CANCELLED", "29A12345", "INVALID_RESERVATION_STATE")]
    [InlineData("PARKING", "29A12345", "INVALID_RESERVATION_STATE")]
    [InlineData("CONFIRMED", "29A54321", "PLATE_MISMATCH")]
    public async Task ReservationArrivalPreservesStateAndPlateGuards(string state, string plate, string code)
    {
        var data = ReservationFixture();
        data.Reservation!.Status = state;
        using var services = CreateServices(data);
        var error = await Assert.ThrowsAsync<GateTransitionException>(() => services.GetRequiredService<IMediator>()
            .Send(new CheckInReservationCommand(data.Site, data.Reservation.Id, plate, null)));
        Assert.Equal(code, error.Code);
        Assert.Equal(state, data.Reservation.Status);
        Assert.Null(data.Session);
        Assert.DoesNotContain("save", data.Calls);
    }

    [Theory]
    [InlineData("duplicate", "VEHICLE_ALREADY_INSIDE")]
    [InlineData("unauthorized", "SITE_ACCESS_DENIED")]
    [InlineData("other-site", "RESERVATION_NOT_FOUND")]
    [InlineData("other-tenant", "RESERVATION_NOT_FOUND")]
    public async Task ReservationArrivalPreservesAccessAndDuplicateGuards(string scenario, string code)
    {
        var data = ReservationFixture();
        data.AlreadyInside = scenario == "duplicate";
        if (scenario == "other-site") data.Reservation!.SiteId = Guid.NewGuid();
        if (scenario == "other-tenant") data.Reservation!.TenantId = Guid.NewGuid();
        var site = scenario == "unauthorized" ? Guid.NewGuid() : data.Site;
        using var services = CreateServices(data);
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => services.GetRequiredService<IMediator>()
            .Send(new CheckInReservationCommand(site, data.Reservation!.Id, "29A12345", null)));
        Assert.Equal(code, error.Code);
        Assert.Equal("CONFIRMED", data.Reservation!.Status);
        Assert.Null(data.Session);
        Assert.DoesNotContain("commit", data.Calls);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ReservationScanAndDepartureKeepExpectedAndActualSlotsSeparate(
        bool allocated, bool scan, bool different)
    {
        var data = ReservationFixture(allocated ? "RESERVED" : null);
        var reservation = data.Reservation!;
        var expected = reservation.TargetSlotId!.Value;
        var actual = different ? Guid.NewGuid() : expected;
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        await mediator.Send(new CheckInReservationCommand(data.Site, reservation.Id, "29A12345", null));
        var session = data.Session!;
        if (scan)
        {
            data.FacilityReserved = !different;
            var result = await mediator.Send(new ScanParkingSessionCommand(data.Site, reservation.Id, actual));
            Assert.Equal(actual, result.CurrentSlotId);
            Assert.Equal(actual, session.CurrentSlotId);
            Assert.NotNull(session.LocationScannedAt);
            if (allocated)
            {
                Assert.Equal(expected, data.Allocation!.SlotId);
                Assert.Equal(different ? "RESERVED" : "OCCUPIED", data.Allocation.AllocationStatus);
                Assert.Equal(different ? (Guid?)null : session.Id, data.Allocation.ParkingSessionId);
            }
        }
        await mediator.Send(new CheckOutReservationCommand(data.Site, reservation.Id, "29A12345", null));
        Assert.Equal("COMPLETED", reservation.Status);
        Assert.NotNull(session.ExitTime);
        Assert.Equal(scan ? actual : (Guid?)null, data.ReleasedSlotId);
        Assert.Equal(allocated ? "RELEASED" : null, data.Allocation?.AllocationStatus);
        var writes = data.Calls.Count(x => x == "save");
        await mediator.Send(new CheckOutReservationCommand(data.Site, reservation.Id, "29A12345", null));
        Assert.Equal(writes, data.Calls.Count(x => x == "save"));
        Assert.Equal(2, data.Events.Count);
        Assert.All(data.Events, e => Assert.Equal(reservation.Id, e.ReservationId));
    }

    [Fact]
    public async Task DifferentObservedSlotRequiresReconciliationForSubsequentClaimsOnEitherBay()
    {
        var data = ReservationFixture("RESERVED");
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        var reservation = data.Reservation!;
        var actual = Guid.NewGuid();
        await mediator.Send(new CheckInReservationCommand(data.Site, reservation.Id, "29A12345", null));
        await mediator.Send(new ScanParkingSessionCommand(data.Site, reservation.Id, actual));

        foreach (var slot in new[] { data.Allocation!.SlotId, actual })
        {
            var error = await Assert.ThrowsAsync<ReservationGateException>(() => mediator.Send(
                new CheckInWalkInCommand(data.Site, "29A54321", null, "CAR", slot)));
            Assert.Equal("OCCUPANCY_RECONCILIATION_REQUIRED", error.Code);
        }
        Assert.Equal(1, data.Calls.Count(call => call == "claim-slot"));
        Assert.Equal(actual, data.Session!.CurrentSlotId);
    }

    [Theory]
    [InlineData(false, "CAPACITY_UNAVAILABLE")]
    [InlineData(true, "NO_AVAILABLE_SLOT")]
    public async Task ReservationScanRejectsAnOccupiedOrReservedAlternateSlot(bool facilityReserved, string code)
    {
        var data = ReservationFixture("RESERVED");
        var actual = Guid.NewGuid();
        data.FacilityReserved = facilityReserved;
        if (!facilityReserved)
            data.OtherSession = new() { Id = Guid.NewGuid(), CurrentSlotId = actual, VehicleType = "CAR" };
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        await mediator.Send(new CheckInReservationCommand(data.Site, data.Reservation!.Id, "29A12345", null));
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => mediator.Send(
            new ScanParkingSessionCommand(data.Site, data.Session!.Id, actual)));
        Assert.Equal(code, error.Code);
        Assert.Null(data.Session!.CurrentSlotId);
        Assert.Null(data.Session.LocationScannedAt);
        Assert.Equal("RESERVED", data.Allocation!.AllocationStatus);
        Assert.Null(data.Allocation.ParkingSessionId);
    }

    [Theory]
    [InlineData("own", "RELEASED")]
    [InlineData("other-reservation", "RESERVED")]
    [InlineData("other-session", "RESERVED")]
    public async Task PaidDepartureReleasesOnlyOwnedAllocations(string owner, string finalStatus)
    {
        var data = ReservationFixture("RESERVED");
        if (owner == "other-session") data.Allocation!.ParkingSessionId = Guid.NewGuid();
        if (owner == "other-reservation") data.Allocation!.ReservationId = Guid.NewGuid();
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        var id = data.Reservation!.Id;
        await mediator.Send(new CheckInReservationCommand(data.Site, id, "29A12345", null));
        var order = Guid.NewGuid();
        data.Session!.AwaitCheckoutPayment(order);
        await mediator.Send(new SimulatePaidGateDepartureCommand(data.Tenant, data.Site, "RESERVATION", id,
            order, data.Session.EntryTime.AddMinutes(5)));
        Assert.Equal(finalStatus, data.Allocation!.AllocationStatus);
        Assert.Equal("COMPLETED", data.Reservation.Status);
        Assert.Equal("COMPLETED", data.Session.SessionStatus);
        Assert.Null(data.ReleasedSlotId);
    }

    private static GateTestRepositories ReservationFixture(string? allocationStatus = null, int offsetHours = 0)
    {
        var data = new GateTestRepositories();
        var now = DateTimeOffset.UtcNow;
        var reservation = data.Reservation = new()
        {
            Id = Guid.NewGuid(), TenantId = data.Tenant, SiteId = data.Site, Status = "CONFIRMED",
            OriginalPlate = "29A-12345", VehicleType = "CAR", TargetSlotId = Guid.NewGuid(),
            ExpectedStartTime = now.AddHours(-1), ExpectedEndTime = now.AddHours(1)
        };
        if (allocationStatus != null)
            data.Allocation = new()
            {
                Id = Guid.NewGuid(), TenantId = data.Tenant, SiteId = data.Site, ReservationId = reservation.Id,
                SlotId = reservation.TargetSlotId.Value, AllocationStatus = allocationStatus,
                AllocatedStartTime = now.AddHours(offsetHours - 1), AllocatedEndTime = now.AddHours(offsetHours + 1)
            };
        return data;
    }

    [Fact]
    public async Task BoundCheckoutPaymentRecordsSettlementAndSimulatesDepartureAtEventTime()
    {
        var data = new GateTestRepositories();
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        var walkIn = await mediator.Send(new CheckInWalkInCommand(data.Site, "29A12345", null, "CAR"));
        var slotId = Guid.NewGuid();
        await mediator.Send(new ScanParkingSessionCommand(data.Site, walkIn.Id, slotId));
        var session = data.Session!;
        Assert.Equal(slotId, session.CurrentSlotId);
        Assert.Null(data.Allocation);
        var orderId = Guid.NewGuid();
        session.AwaitCheckoutPayment(orderId);
        var occurredAt = session.LocationScannedAt!.Value.AddMinutes(10);
        var payment = new SimulatePaidGateDepartureCommand(data.Tenant, data.Site, "PARKING_SESSION", walkIn.Id, orderId, occurredAt);
        await mediator.Send(payment with { PaymentOrderId = Guid.NewGuid() });
        Assert.Null(session.ExitTime);
        Assert.Null(session.CheckoutPaidAt);
        Assert.DoesNotContain("queue-snapshot", data.Calls);

        await mediator.Send(payment);
        Assert.Equal(occurredAt, session.CheckoutPaidAt);
        Assert.Equal(occurredAt, session.ExitTime);
        Assert.Equal("COMPLETED", session.SessionStatus);
        Assert.Null(data.Allocation);
        Assert.Equal(slotId, data.ReleasedSlotId);
        Assert.Equal(new[] { "queue-snapshot", "save", "commit", "release-slot" }, data.Calls.TakeLast(4));
        Assert.Equal("release-slot", data.Calls.Last());

        await mediator.Send(payment with { OccurredAt = occurredAt.AddMinutes(1) });
        Assert.Equal(occurredAt, session.ExitTime);
    }

    [Fact]
    public async Task AuditCaptureFailurePreventsSaveAndCommit()
    {
        var data = new GateTestRepositories { FailAudit = true };
        using var services = CreateServices(data);
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => services.GetRequiredService<IMediator>()
            .Send(new CheckInWalkInCommand(data.Site, "29A12345", null, "CAR")));
        Assert.Equal(503, error.StatusCode);
        Assert.DoesNotContain("save", data.Calls);
        Assert.DoesNotContain("commit", data.Calls);
    }

    [Fact]
    public void DomainRejectsDepartureBeforeEntryAndMismatchedPlate()
    {
        var session = new ParkingSessionRow { EntryTime = DateTimeOffset.UtcNow, SessionStatus = "ACTIVE" };
        Assert.Throws<GateTransitionException>(() => session.RecordDeparture(session.EntryTime.AddSeconds(-1), null));
        Assert.Null(session.ExitTime);
        Assert.Throws<GateTransitionException>(() => GateRules.VerifyPlate("29A12345", "29A54321"));
    }

    private static ServiceProvider CreateServices(GateTestRepositories data)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddApplicationServices(); services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOperatorIdentityClient>(data); services.AddSingleton<IOperationsConcurrency>(data);
        services.AddSingleton<OperationsUnitOfWork>(data); services.AddSingleton<IReservationRepository>(data);
        services.AddSingleton<IParkingSessionRepository>(data); services.AddSingleton<ISlotAllocationRepository>(data);
        services.AddSingleton<IOperationsAuditPublisher>(data);
        services.AddSingleton<IOperatorGateSlotClaims>(data);
        services.AddSingleton<IOccupancyOutboxStore>(data);
        services.AddSingleton<IReservationAvailabilityRepository>(data);
        return services.BuildServiceProvider();
    }
}
