using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

using OperationsUnitOfWork = SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWork;
using OperationsUnitOfWorkTransaction = SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWorkTransaction;

namespace SmartParking.FacilityService.Tests;

internal sealed class GateTestRepositories : IReservationRepository, IParkingSessionRepository, ISlotAllocationRepository,
    IOperationsAuditPublisher, IOperatorIdentityClient, IOperationsConcurrency, OperationsUnitOfWork, OperationsUnitOfWorkTransaction,
    IOperatorGateSlotClaims, IOccupancyOutboxStore, IReservationAvailabilityRepository
{
    public Guid Site { get; } = Guid.NewGuid();
    public Guid Tenant { get; } = Guid.NewGuid();
    public List<string> Calls { get; } = [];
    public ReservationRow? Reservation;
    public ParkingSessionRow? Session;
    public SlotAllocationRow? Allocation;
    public ParkingSessionRow? OtherSession;
    public bool FacilityReserved;
    public bool AlreadyInside;
    public bool FailAudit;
    public bool SlotUnavailable;
    public Guid? ReleasedSlotId;
    public Task<OperatorSiteScope> RequireSiteAsync(Guid site, CancellationToken ct)
    {
        Calls.Add("identity");
        if (site != Site) throw new ReservationGateException(403, "SITE_ACCESS_DENIED", "Site access denied.");
        return Task.FromResult(new OperatorSiteScope(Guid.NewGuid(), Tenant, Site));
    }
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
    public void AddSnapshotRequired(Guid siteId, DateTimeOffset occurredAt)
    {
        Assert.Equal(Site, siteId);
        Calls.Add("queue-snapshot");
    }

    public Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(int batchSize, TimeSpan leaseDuration, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task MarkPublishedAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct) =>
        throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task<IOperatorGateSlotLease> ClaimAvailableAsync(Guid tenantId, Guid siteId, string vehicleType,
        Guid? preferredSlotId, Guid? targetUnitId, bool allowReservedSlot, CancellationToken ct)
    {
        Calls.Add("claim-slot");
        if (SlotUnavailable || (FacilityReserved && !allowReservedSlot))
            throw new ReservationGateException(409, "NO_AVAILABLE_SLOT", "No eligible parking slot is available.");
        return Task.FromResult<IOperatorGateSlotLease>(new GateTestSlotLease(new ClaimedParkingSlot(
            Assert.IsType<Guid>(preferredSlotId), "A01", vehicleType)));
    }
    public Task ReleaseAsync(Guid tenantId, Guid siteId, Guid slotId, CancellationToken ct)
    {
        Calls.Add("release-slot");
        ReleasedSlotId = slotId;
        return Task.CompletedTask;
    }

    public Task<ReservationAvailabilityFacts> ReadPhysicalAsync(ParkingSessionRow session, DateTimeOffset at, CancellationToken ct)
    {
        Assert.True(Calls.LastIndexOf("guard") > Calls.LastIndexOf("begin"));
        Calls.Add("availability");
        return Task.FromResult(new ReservationAvailabilityFacts(Reservation is null ? [] : [Reservation],
            new[] { Session, OtherSession }.OfType<ParkingSessionRow>().Where(s => s.ExitTime == null).ToArray(),
            Allocation is { AllocationStatus: "RESERVED" or "OCCUPIED" } && Allocation.AllocatedStartTime <= at &&
                at < Allocation.AllocatedEndTime ? [Allocation] : [], 0));
    }

    public Task<ReservationAvailabilityFacts> ReadAsync(ReservationRow proposed, DateTimeOffset at, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task LockAccountAsync(Guid accountId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasUnclassifiedCancellationAsync(Guid accountId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ReservationCancellationEvidence>> ReadCancellationsAsync(Guid accountId, DateTimeOffset dayStart,
        DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public IReservationRepository Reservations => this;
    public IParkingSessionRepository ParkingSessions => this;
    public ISlotAllocationRepository SlotAllocations => this;
    Task<ReservationRow?> IReservationRepository.FindForUpdateAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => Task.FromResult(ScopedReservation(tenant, site, id));
    Task<ParkingSessionRow?> IParkingSessionRepository.FindForUpdateAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) =>
        Task.FromResult(Session?.Id == id ? Session : null);
    public Task<bool> HasActivePlateAsync(Guid tenant, Guid site, string plate, CancellationToken ct) { Calls.Add("active-read"); return Task.FromResult(AlreadyInside); }
    public Task<ParkingSessionRow?> FindByReservationAsync(Guid tenant, Guid site, Guid id, bool active, CancellationToken ct) =>
        Task.FromResult(Session is not null && Session.ReservationId == id && (Session.ExitTime is null) == active ? Session : null);
    public Task<IReadOnlyList<SlotAllocationRow>> FindActiveForVisitAsync(
        Guid tenant, Guid site, Guid sessionId, Guid? reservationId, CancellationToken ct)
    {
        Calls.Add("read-allocations");
        var a = Allocation;
        if (a == null || a.TenantId != tenant || a.SiteId != site || a.AllocationStatus is not ("RESERVED" or "OCCUPIED"))
            return Task.FromResult<IReadOnlyList<SlotAllocationRow>>([]);
        var linked = a.ParkingSessionId == sessionId && (a.ReservationId == null || a.ReservationId == reservationId);
        var unlinked = reservationId.HasValue && a.ReservationId == reservationId && a.ParkingSessionId == null &&
            a.AllocationStatus == "RESERVED";
        return Task.FromResult<IReadOnlyList<SlotAllocationRow>>(linked || unlinked ? [a] : []);
    }
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
    public Task<ReservationGateEntry?> FindByIdAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => Task.FromResult<ReservationGateEntry?>(ScopedReservation(tenant, site, id) is null ? null : new(id, Guid.NewGuid(), site, "site", Allocation?.SlotId, "Unassigned", "29A12345", Reservation!.Status,
        null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, DateTimeOffset.UtcNow));
    private ReservationRow? ScopedReservation(Guid tenant, Guid site, Guid id) =>
        Reservation?.Id == id && Reservation.TenantId == tenant && Reservation.SiteId == site ? Reservation : null;

    public Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsAsync(Guid tenant, Guid site, CancellationToken ct) => Task.FromResult<IReadOnlyList<ParkingSessionHistoryEntry>>([]);
    public Task<WalkInParkingSession?> FindActiveWalkInAsync(Guid tenant, Guid site, string plate, CancellationToken ct) => Task.FromResult<WalkInParkingSession?>(null);
}
