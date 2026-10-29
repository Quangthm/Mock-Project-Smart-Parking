using System.Net;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Infrastructure;

namespace SmartParking.ParkingService.Tests;

// Focused coverage for the two failure modes that used to wedge a site forever:
// a preparation abandoned between PrepareAsync and PublishAsync, and an aborting
// row whose ReservationService hold token no longer matches any durable row.
public sealed class StructureHoldReleaseTests
{
    [Fact]
    public async Task FailureAfterPreparationReleasesHoldAndCompletesDurableRow()
    {
        var fixture = new Fixture();

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            fixture.Service.ChangeAsync(fixture.Owner, fixture.SiteId,
                _ => throw new StructureException("STRUCTURE_CONFLICT", "boom"), CancellationToken.None));

        Assert.Equal("STRUCTURE_CONFLICT", error.Code);
        Assert.False(fixture.Commitments.Hold);
        Assert.Equal(1, fixture.Commitments.ForceReleaseCalls);
        Assert.Equal(0, fixture.Commitments.ReleaseCalls);
        Assert.DoesNotContain(fixture.Store.Rows, row => row.State != "completed");
        Assert.Empty(await fixture.Store.PendingAsync(fixture.SiteId, CancellationToken.None));
    }

    [Fact]
    public async Task FailedCompensationDoesNotMaskTheOriginalFailure()
    {
        var fixture = new Fixture { CompensationFails = true };

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            fixture.Service.ChangeAsync(fixture.Owner, fixture.SiteId,
                _ => throw new StructureException("STRUCTURE_CONFLICT", "boom"), CancellationToken.None));

        Assert.Equal("STRUCTURE_CONFLICT", error.Code);
        Assert.Equal(1, fixture.Commitments.ForceReleaseCalls);
    }

    [Fact]
    public async Task AbortingRowForceReleasesAndReadyRowReleasesExactOutcome()
    {
        var aborting = new SynchronizationFixture("aborting", null);
        await aborting.Service.PublishAsync(aborting.Row, CancellationToken.None);

        Assert.Equal(1, aborting.Commitments.ForceReleaseCalls);
        Assert.Equal(0, aborting.Commitments.ReleaseCalls);
        Assert.Equal("completed", aborting.Store.Rows.Single().State);

        var outcome = new StructureOutcome(true, [], [], []);
        var ready = new SynchronizationFixture("ready", outcome);
        await ready.Service.PublishAsync(ready.Row, CancellationToken.None);

        Assert.Equal(0, ready.Commitments.ForceReleaseCalls);
        Assert.Equal(1, ready.Commitments.ReleaseCalls);
        Assert.Same(outcome, ready.Commitments.LastOutcome);
        Assert.Equal("completed", ready.Store.Rows.Single().State);
    }

    [Fact]
    public async Task AbandonedPreparationIsFencedAndForceReleased()
    {
        var pending = new SynchronizationFixture("preparing", null);

        await pending.Service.PublishAsync(pending.Row, CancellationToken.None);

        Assert.Equal("aborting", pending.Store.FencedState);
        Assert.Equal(1, pending.Commitments.ForceReleaseCalls);
        Assert.Equal(0, pending.Commitments.ReleaseCalls);
        Assert.Equal("completed", pending.Store.Rows.Single().State);
    }

    [Fact]
    public async Task ReleaseConflictSurfacesAsStructureBusy()
    {
        var client = new StructureCommitmentsClient(Client(HttpStatusCode.Conflict));

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            client.ReleaseAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None));

        Assert.Equal("STRUCTURE_BUSY", error.Code);
        Assert.NotEqual("Reservation synchronization is pending; retry after recovery.", error.Message);
    }

    [Fact]
    public async Task ReleaseServerFailureRemainsCommitmentsUnavailable()
    {
        var client = new StructureCommitmentsClient(Client(HttpStatusCode.InternalServerError));

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            client.ReleaseAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None));

        Assert.Equal("COMMITMENTS_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task ForceReleaseConflictSurfacesAsStructureBusy()
    {
        var client = new StructureCommitmentsClient(Client(HttpStatusCode.Conflict));

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            client.ForceReleaseAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("STRUCTURE_BUSY", error.Code);
    }

    private static HttpClient Client(HttpStatusCode status) =>
        new(new StatusHandler(status)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class Fixture : IOwnerAuthorizer, ISlotOperatorAuthorizer, IParkingStructureRepository
    {
        public OwnerScope Owner { get; } = new(Guid.NewGuid(), Guid.NewGuid());
        public Guid SiteId { get; } = Guid.NewGuid();
        public ParkingStructure Structure { get; }
        public FakeStore Store { get; } = new();
        public FakeCommitments Commitments { get; } = new();
        public ParkingStructureService Service { get; }

        public Fixture()
        {
            Structure = new(new SiteProfile(SiteId, Owner.TenantId, "TEST", "Test", "Address", null, null, true, 0));
            var zone = Structure.AddUnit(UnitType.ZONE, "A", maxCapacity: 1);
            Structure.AddSlot(zone, "A01", VehicleType.CAR);
            Service = new(this, this, this, Commitments, Store,
                new StructureSynchronizationService(Store, Commitments));
        }

        public bool CompensationFails
        {
            set => Commitments.FailForceRelease = value;
        }

        public Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken ct)
            => Task.FromResult(owner == Owner);

        public Task<OwnerScope> ScopeAsync(Guid site, CancellationToken ct)
            => Task.FromResult(Owner);

        public Task<bool> IsAuthorizedAsync(OwnerScope actor, Guid site, CancellationToken ct)
            => Task.FromResult(actor == Owner && site == SiteId);

        public Task<ParkingStructure> ReadAsync(
            OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, CancellationToken ct)
            => Task.FromResult(Structure);

        public Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SiteProfile>>([Structure.Site]);

        public Task<SiteProfile> CreateAsync(OwnerScope owner, SiteProfile site, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IParkingStructureEdit> BeginEditAsync(
            OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, Guid? operationId, CancellationToken ct)
            => Task.FromResult<IParkingStructureEdit>(new FakeEdit(Structure));

        private sealed class FakeEdit(ParkingStructure structure) : IParkingStructureEdit
        {
            public ParkingStructure Structure => structure;

            public Task CommitAsync(
                Guid synchronizationToken, StructureOutcome outcome,
                Guid? markedSlot, string? reason, CancellationToken ct) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class SynchronizationFixture
    {
        public FakeStore Store { get; } = new();
        public FakeCommitments Commitments { get; } = new();
        public StructureSynchronization Row { get; }
        public StructureSynchronizationService Service { get; }

        public SynchronizationFixture(string state, StructureOutcome? outcome)
        {
            Row = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, state, outcome);
            Store.Rows.Add(Row);
            Service = new(Store, Commitments);
        }
    }

    private sealed class FakeStore : IStructureSynchronizationStore
    {
        public List<StructureSynchronization> Rows { get; } = [];
        public string? FencedState { get; private set; }

        public Task<IAsyncDisposable?> TryLockSiteAsync(Guid siteId, CancellationToken ct)
            => Task.FromResult<IAsyncDisposable?>(new Gate());

        public Task PrepareAsync(StructureSynchronization synchronization, CancellationToken ct)
        {
            Rows.Add(synchronization);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StructureSynchronization>> PendingAsync(
            Guid? siteId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<StructureSynchronization>>(
                Rows.Where(row => row.State != "completed" && (siteId is null || row.SiteId == siteId))
                    .ToArray());

        public Task<StructureSynchronization> FencePreparationAsync(Guid token, CancellationToken ct)
        {
            var index = Rows.FindIndex(row => row.Token == token);
            if (Rows[index].State == "preparing")
                Rows[index] = Rows[index] with { State = "aborting" };
            FencedState = Rows[index].State;
            return Task.FromResult(Rows[index]);
        }

        public Task AcknowledgeAsync(Guid token, CancellationToken ct)
        {
            var index = Rows.FindIndex(row => row.Token == token);
            Rows[index] = Rows[index] with { State = "completed" };
            return Task.CompletedTask;
        }

        private sealed class Gate : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FakeCommitments : IStructureCommitments
    {
        public int AcquireCalls { get; private set; }
        public int ReleaseCalls { get; private set; }
        public int ForceReleaseCalls { get; private set; }
        public StructureOutcome? LastOutcome { get; private set; }
        public bool Hold { get; private set; }
        public bool FailForceRelease { get; set; }

        public Task<CommitmentSnapshot> ReadSnapshotAsync(
            Guid tenantId, Guid siteId, CancellationToken cancellationToken)
            => Task.FromResult(new CommitmentSnapshot([], false));

        public Task<CommitmentSnapshot> AcquireAsync(
            Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken)
        {
            AcquireCalls++;
            Hold = true;
            return Task.FromResult(new CommitmentSnapshot([], false));
        }

        public Task ReleaseAsync(
            Guid tenantId, Guid siteId, Guid token, StructureOutcome? outcome,
            CancellationToken cancellationToken)
        {
            ReleaseCalls++;
            LastOutcome = outcome;
            Hold = false;
            return Task.CompletedTask;
        }

        public Task ForceReleaseAsync(
            Guid tenantId, Guid siteId, CancellationToken cancellationToken)
        {
            ForceReleaseCalls++;
            if (FailForceRelease)
                throw new StructureException(
                    "COMMITMENTS_UNAVAILABLE", "Reservation synchronization is pending; retry after recovery.");
            Hold = false;
            return Task.CompletedTask;
        }
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status));
    }
}
