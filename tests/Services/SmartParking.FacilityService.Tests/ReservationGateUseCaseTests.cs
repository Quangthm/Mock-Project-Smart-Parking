using FluentValidation;
using SmartParking.Contracts.Audit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Application.Usecase.ReservationGate;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;

using OperationsUnitOfWork = SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWork;
using OperationsUnitOfWorkTransaction = SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWorkTransaction;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationGateUseCaseTests
{
    [Fact]
    public async Task MediatorRejectsInvalidInputBeforeIdentityOrPersistence()
    {
        var data = new FakeRepositories();
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(new CheckInWalkInCommand(data.Site, "!", "http://invalid", "HELICOPTER")));
        Assert.Empty(data.Calls);
    }

    [Fact]
    public async Task WalkInAcquiresGuardBeforeReadAndCommitsOnce()
    {
        var data = new FakeRepositories();
        using var services = CreateServices(data);
        var result = await services.GetRequiredService<IMediator>().Send(new CheckInWalkInCommand(data.Site, "29A-12345", null, "CAR"));
        Assert.Equal("29A12345", data.Session!.NormalizedPlate);
        Assert.Equal(data.Session.Id, result.Id);
        Assert.Equal(new[] { "identity", "begin", "plate-guard", "guard", "active-read", "add-session", "audit", "save", "commit" }, data.Calls);
    }

    [Fact]
    public async Task DuplicateVehicleDoesNotWriteOrCommit()
    {
        var data = new FakeRepositories { AlreadyInside = true };
        using var services = CreateServices(data);
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => services.GetRequiredService<IMediator>()
            .Send(new CheckInWalkInCommand(data.Site, "29A-12345", null, "CAR")));
        Assert.Equal("VEHICLE_ALREADY_INSIDE", error.Code);
        Assert.DoesNotContain("save", data.Calls);
        Assert.DoesNotContain("commit", data.Calls);
    }

    [Fact]
    public async Task PreferredSlotWithoutAllocationCannotBecomeActualParking()
    {
        var data = new FakeRepositories();
        data.Reservation = new() { Id = Guid.NewGuid(), Status = "CONFIRMED", TargetSlotId = Guid.NewGuid(), OriginalPlate = "29A-12345" };
        using var services = CreateServices(data);
        var error = await Assert.ThrowsAsync<ReservationGateException>(() => services.GetRequiredService<IMediator>()
            .Send(new CheckInReservationCommand(data.Site, data.Reservation.Id, "29A-12345", null)));
        Assert.Equal("ALLOCATION_UNAVAILABLE", error.Code);
        Assert.Equal("CONFIRMED", data.Reservation.Status);
        Assert.Null(data.Session);
        Assert.DoesNotContain("save", data.Calls);
    }

    [Fact]
    public async Task ReservedSlotBecomesOccupiedOnlyAtScanAndIsReleasedAtDeparture()
    {
        var data = new FakeRepositories();
        data.Reservation = new() { Id = Guid.NewGuid(), Status = "CONFIRMED", OriginalPlate = "29A-12345", VehicleType = "CAR" };
        data.Allocation = new() { Id = Guid.NewGuid(), ReservationId = data.Reservation.Id, SlotId = Guid.NewGuid(), AllocationStatus = "RESERVED",
            AllocatedStartTime = DateTimeOffset.UtcNow.AddHours(-1), AllocatedEndTime = DateTimeOffset.UtcNow.AddHours(1) };
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        await mediator.Send(new CheckInReservationCommand(data.Site, data.Reservation.Id, "29A12345", null));
        Assert.Equal("PARKING", data.Reservation.Status);
        Assert.Null(data.Session!.CurrentSlotId);
        Assert.Equal("RESERVED", data.Allocation.AllocationStatus);
        var scan = new ScanParkingSessionCommand(data.Site, data.Reservation.Id, data.Allocation.SlotId);
        await mediator.Send(scan);
        Assert.Equal(data.Allocation.SlotId, data.Session!.CurrentSlotId);
        Assert.Equal("OCCUPIED", data.Allocation.AllocationStatus);
        Assert.NotNull(data.Session.LocationScannedAt);
        await mediator.Send(new CheckOutReservationCommand(data.Site, data.Reservation.Id, "29A12345", null));
        Assert.Equal("COMPLETED", data.Reservation.Status);
        Assert.NotNull(data.Session.ExitTime);
        Assert.Equal("RELEASED", data.Allocation.AllocationStatus);
        var writes = data.Calls.Count(x => x == "save");
        await mediator.Send(new CheckOutReservationCommand(data.Site, data.Reservation.Id, "29A12345", null));
        Assert.Equal(writes, data.Calls.Count(x => x == "save"));
        Assert.Equal(2, data.Events.Count);
        Assert.All(data.Events, e => Assert.Equal(data.Reservation.Id, e.ReservationId));
    }

    [Fact]
    public async Task BoundCheckoutPaymentRecordsSettlementAndSimulatesDepartureAtEventTime()
    {
        var data = new FakeRepositories();
        using var services = CreateServices(data);
        var mediator = services.GetRequiredService<IMediator>();
        var walkIn = await mediator.Send(new CheckInWalkInCommand(data.Site, "29A12345", null, "CAR"));
        var slotId = Guid.NewGuid();
        await mediator.Send(new ScanParkingSessionCommand(data.Site, walkIn.Id, slotId));
        var session = data.Session!;
        var orderId = Guid.NewGuid();
        session.AwaitCheckoutPayment(orderId);
        var occurredAt = session.LocationScannedAt!.Value.AddMinutes(10);
        var payment = new SimulatePaidGateDepartureCommand(data.Tenant, data.Site, "PARKING_SESSION", walkIn.Id, orderId, occurredAt);
        await mediator.Send(payment with { PaymentOrderId = Guid.NewGuid() });
        Assert.Null(session.ExitTime);
        Assert.Null(session.CheckoutPaidAt);

        await mediator.Send(payment);
        Assert.Equal(occurredAt, session.CheckoutPaidAt);
        Assert.Equal(occurredAt, session.ExitTime);
        Assert.Equal("COMPLETED", session.SessionStatus);
        Assert.Equal("RELEASED", data.Allocation!.AllocationStatus);
        Assert.Equal("release-slot", data.Calls.Last());

        await mediator.Send(payment with { OccurredAt = occurredAt.AddMinutes(1) });
        Assert.Equal(occurredAt, session.ExitTime);
    }

    [Fact]
    public async Task AuditCaptureFailurePreventsSaveAndCommit()
    {
        var data = new FakeRepositories { FailAudit = true };
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

    private static ServiceProvider CreateServices(FakeRepositories data)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddApplicationServices(); services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOperatorIdentityClient>(data); services.AddSingleton<IOperationsConcurrency>(data);
        services.AddSingleton<OperationsUnitOfWork>(data); services.AddSingleton<IReservationRepository>(data);
        services.AddSingleton<IParkingSessionRepository>(data); services.AddSingleton<ISlotAllocationRepository>(data);
        services.AddSingleton<IOperationsAuditPublisher>(data);
        services.AddSingleton<IOperatorGateSlotClaims>(data);
        return services.BuildServiceProvider();
    }

    private sealed class FakeRepositories : IReservationRepository, IParkingSessionRepository, ISlotAllocationRepository,
        IOperationsAuditPublisher, IOperatorIdentityClient, IOperationsConcurrency, OperationsUnitOfWork, OperationsUnitOfWorkTransaction, IOperatorGateSlotClaims
    {
        public Guid Site { get; } = Guid.NewGuid();
        public Guid Tenant { get; } = Guid.NewGuid();
        public List<string> Calls { get; } = [];
        public ReservationRow? Reservation;
        public ParkingSessionRow? Session;
        public SlotAllocationRow? Allocation;
        public bool AlreadyInside;
        public bool FailAudit;
        public Task<OperatorSiteScope> RequireSiteAsync(Guid site, CancellationToken ct) { Calls.Add("identity"); return Task.FromResult(new OperatorSiteScope(Guid.NewGuid(), Tenant, Site)); }
        public Task<OperatorSiteScope> AuthorizeSiteAsync(Guid site, string bearerToken, CancellationToken ct) => RequireSiteAsync(site, ct);
        public Task LockSiteAsync(Guid site, CancellationToken ct) { Calls.Add("guard"); return Task.CompletedTask; }
        public Task LockVehicleAsync(string id, CancellationToken ct)
        {
            Assert.Equal(GateRules.NormalizePlate(id), id);
            Assert.True(Calls.LastIndexOf("guard") < Calls.LastIndexOf("begin"));
            Calls.Add("plate-guard");
            return Task.CompletedTask;
        }
        public Task<OperationsUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) { Calls.Add("begin"); return Task.FromResult<OperationsUnitOfWorkTransaction>(this); }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) { Calls.Add("save"); return Task.FromResult(1); }
        public Task CommitAsync(CancellationToken ct) { Calls.Add("commit"); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<IOperatorGateSlotLease> ClaimAvailableAsync(Guid tenantId, Guid siteId, string vehicleType,
            Guid? preferredSlotId, Guid? targetUnitId, bool allowReservedSlot, CancellationToken ct) =>
            Task.FromResult<IOperatorGateSlotLease>(new SlotLease(new ClaimedParkingSlot(preferredSlotId!.Value, "A01", vehicleType)));
        public Task ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct)
        {
            Calls.Add("release-slot");
            return Task.CompletedTask;
        }
        private sealed class SlotLease(ClaimedParkingSlot slot) : IOperatorGateSlotLease
        {
            public ClaimedParkingSlot Slot => slot;
            public void Commit() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public IReservationRepository Reservations => this;
        public IParkingSessionRepository ParkingSessions => this;
        public ISlotAllocationRepository SlotAllocations => this;
        Task<ReservationRow?> IReservationRepository.FindForUpdateAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => Task.FromResult(Reservation);
        Task<ParkingSessionRow?> IParkingSessionRepository.FindForUpdateAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) =>
            Task.FromResult(Session?.Id == id ? Session : null);
        public Task<bool> HasActivePlateAsync(Guid tenant, Guid site, string plate, CancellationToken ct) { Calls.Add("active-read"); return Task.FromResult(AlreadyInside); }
        public Task<ParkingSessionRow?> FindByReservationAsync(Guid tenant, Guid site, Guid id, bool active, CancellationToken ct) =>
            Task.FromResult(Session is not null && Session.ReservationId == id && (Session.ExitTime is null) == active ? Session : null);
        public Task<SlotAllocationRow?> FindReservedAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => Task.FromResult(Allocation);
        public Task<IReadOnlyList<SlotAllocationRow>> FindActiveForSessionAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<SlotAllocationRow>>(Allocation is { AllocationStatus: "RESERVED" or "OCCUPIED" } ? [Allocation] : []);
        public Task AddAsync(ParkingSessionRow row, CancellationToken ct) { Calls.Add("add-session"); Session = row; return Task.CompletedTask; }
        public Task AddAsync(ReservationRow row, CancellationToken ct) { Reservation = row; return Task.CompletedTask; }
        public Task AddAsync(SlotAllocationRow row, CancellationToken ct) { Allocation = row; return Task.CompletedTask; }
        public List<OperationsGateAuditV1> Events { get; } = [];
        public Task PublishAsync(OperationsGateAuditV1 row, CancellationToken ct)
        {
            if (FailAudit) throw new ReservationGateException(503, "AUDIT_PUBLISHING_UNAVAILABLE", "Audit is unavailable.");
            Calls.Add("audit"); Events.Add(row); return Task.CompletedTask;
        }
        public void Remove(ReservationRow row) => throw new NotSupportedException();
        public void Remove(ParkingSessionRow row) => throw new NotSupportedException();
        public void Remove(SlotAllocationRow row) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenant, Guid site, CancellationToken ct) => Task.FromResult<IReadOnlyList<ReservationGateEntry>>([]);
        public Task<ReservationGateEntry?> FindAsync(Guid tenant, Guid site, string query, CancellationToken ct) => Task.FromResult<ReservationGateEntry?>(null);
        public Task<ReservationGateEntry?> FindByIdAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => Task.FromResult<ReservationGateEntry?>(new(id, Guid.NewGuid(), site, "site", Allocation?.SlotId, "Unassigned", "29A12345", Reservation!.Status,
            null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, DateTimeOffset.UtcNow));
        public Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsAsync(Guid tenant, Guid site, CancellationToken ct) => Task.FromResult<IReadOnlyList<ParkingSessionHistoryEntry>>([]);
        public Task<WalkInParkingSession?> FindActiveWalkInAsync(Guid tenant, Guid site, string plate, CancellationToken ct) => Task.FromResult<WalkInParkingSession?>(null);
    }
}
