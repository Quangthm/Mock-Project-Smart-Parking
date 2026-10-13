using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using ReservationDomain = SmartParking.ReservationService.Domain;

namespace SmartParking.ParkingService.Tests;

public sealed class ApplicationWorkflowTests
{
    [Fact]
    public async Task EditAcquiresBeforeTransactionAndPublishesAfterDisposal()
    {
        var fixture = new WorkflowFixture();
        await fixture.Service.ChangeAsync(fixture.Owner, fixture.SiteId,
            structure => structure.RemoveSlot(fixture.SlotId));

        Assert.Equal(new[]
        {
            "authorize", "lock", "prepare", "acquire", "begin",
            "commit", "dispose-edit", "release-outcome", "acknowledge", "unlock"
        }, fixture.Events);
        Assert.Empty(fixture.Structure.Slots);
        Assert.Equal(fixture.AcquiredToken, fixture.Store.Rows.Single().Token);
        Assert.Equal("completed", fixture.Store.Rows.Single().State);
    }

    [Fact]
    public async Task DomainRejectionNeverCommitsOrPublishesAnOutcome()
    {
        var fixture = new WorkflowFixture();
        var error = await Assert.ThrowsAsync<StructureException>(() =>
            fixture.Service.ChangeAsync(fixture.Owner, fixture.SiteId,
                structure => structure.RemoveSlot(Guid.NewGuid())));

        Assert.Equal("SLOT_NOT_FOUND", error.Code);
        Assert.DoesNotContain("commit", fixture.Events);
        Assert.DoesNotContain("release-outcome", fixture.Events);

        // A later worker can abort this preparation without claiming an edit completed.
        await fixture.Synchronization.RecoverPendingAsync(CancellationToken.None);
        Assert.Equal("completed", fixture.Store.Rows.Single().State);
        Assert.Contains("release-abort", fixture.Events);
    }

    [Fact]
    public async Task AuthorizationFailureNeverCreatesAHold()
    {
        var fixture = new WorkflowFixture { Authorized = false };
        var error = await Assert.ThrowsAsync<StructureException>(() =>
            fixture.Service.ChangeAsync(fixture.Owner, fixture.SiteId, structure => structure.RemoveSlot(fixture.SlotId)));

        Assert.Equal("FORBIDDEN", error.Code);
        Assert.Empty(fixture.Store.Rows);
        Assert.Equal(new[] { "authorize" }, fixture.Events);
    }

    [Fact]
    public async Task LostPublicationResponsePreservesExactCommittedPayloadForRestart()
    {
        var fixture = new WorkflowFixture { FailRelease = true };
        await Assert.ThrowsAsync<StructureException>(() => fixture.Service.ChangeAsync(
            fixture.Owner, fixture.SiteId, structure => structure.RemoveSlot(fixture.SlotId)));

        var committed = Assert.Single(fixture.Store.Rows);
        Assert.Equal("ready", committed.State);
        Assert.Equal(new[] { fixture.SlotId }, committed.Outcome!.RemovedSlots);
        Assert.Empty(fixture.Structure.Slots);

        fixture.FailRelease = false;
        var restarted = new StructureSynchronizationService(fixture.Store, fixture);
        var failures = await restarted.RecoverPendingAsync(CancellationToken.None);

        Assert.Empty(failures);
        Assert.Equal("completed", fixture.Store.Rows.Single().State);
        Assert.Equal(1, fixture.Events.Count(step => step == "commit"));
        Assert.Equal(2, fixture.Events.Count(step => step == "release-outcome"));
    }

    [Fact]
    public async Task RecoverySkipsASiteWhoseRequestStillOwnsTheGate()
    {
        var fixture = new WorkflowFixture();
        fixture.Store.Rows.Add(new(Guid.NewGuid(), fixture.Owner.TenantId, fixture.SiteId, null, "preparing", null));
        fixture.Store.LockAvailable = false;

        var failures = await fixture.Synchronization.RecoverPendingAsync(CancellationToken.None);

        Assert.Empty(failures);
        Assert.Equal("preparing", fixture.Store.Rows.Single().State);
        Assert.DoesNotContain("release-abort", fixture.Events);
    }

    [Fact]
    public async Task RecoveryUsesCommittedOutcomeWhenPhysicalCommitWinsTheFenceRace()
    {
        var fixture = new WorkflowFixture();
        var outcome = new StructureOutcome(true, [fixture.SlotId], [], []);
        fixture.Store.Rows.Add(new(Guid.NewGuid(), fixture.Owner.TenantId, fixture.SiteId, null, "preparing", null));
        fixture.Store.CommittedDuringFence = outcome;

        await fixture.Synchronization.RecoverPendingAsync(CancellationToken.None);

        Assert.Contains("release-outcome", fixture.Events);
        Assert.DoesNotContain("release-abort", fixture.Events);
        Assert.Equal("completed", fixture.Store.Rows.Single().State);
    }

    [Fact]
    public async Task OneFailingSiteDoesNotStarveAnotherSite()
    {
        var fixture = new WorkflowFixture();
        var otherSite = Guid.NewGuid();
        fixture.Store.Rows.Add(new(Guid.NewGuid(), fixture.Owner.TenantId, fixture.SiteId, null, "ready",
            new(true, [], [], [])));
        fixture.Store.Rows.Add(new(Guid.NewGuid(), fixture.Owner.TenantId, otherSite, null, "ready",
            new(true, [], [], [])));
        fixture.FailingSite = fixture.SiteId;

        var failures = await fixture.Synchronization.RecoverPendingAsync(CancellationToken.None);

        Assert.Single(failures);
        Assert.Equal("ready", fixture.Store.Rows.Single(row => row.SiteId == fixture.SiteId).State);
        Assert.Equal("completed", fixture.Store.Rows.Single(row => row.SiteId == otherSite).State);
    }

    [Fact]
    public async Task ReadAlsoRecoversItsHoldAndDoesNotBeginAnEdit()
    {
        var fixture = new WorkflowFixture();
        await fixture.Service.GetAsync(fixture.Owner, fixture.SiteId);

        Assert.DoesNotContain("begin", fixture.Events);
        Assert.Contains("release-abort", fixture.Events);
        Assert.Equal("completed", fixture.Store.Rows.Single().State);
    }


    [Fact]
    public async Task OperationalAssignmentRevokedDuringAcquirePreventsPhysicalCommit()
    {
        var fixture = new WorkflowFixture { RevokeDuringAcquire = true };
        var error = await Assert.ThrowsAsync<StructureException>(() =>
            fixture.Service.MarkOperationalBackupAsync(
                fixture.Owner, fixture.SiteId, fixture.SlotId, "Temporary backup", CancellationToken.None));

        Assert.Equal("FORBIDDEN", error.Code);
        Assert.DoesNotContain("begin", fixture.Events);
        Assert.DoesNotContain("commit", fixture.Events);
        Assert.Null(fixture.Structure.Slots.Single().ReservationState);
        Assert.Equal(2, fixture.Events.Count(step => step == "authorize"));
    }

    [Fact]
    public void ReservationRejectsInvalidOrDuplicateCapacityPools()
    {
        var invalidBackup = new ReservationDomain.EditOutcome(true, [], [],
            [new(null, "CAR", 1, 2)]);
        var duplicates = new ReservationDomain.EditOutcome(true, [], [],
            [new(null, "CAR", 1), new(null, "CAR", 2)]);
        var nullEntry = new ReservationDomain.EditOutcome(true, [], [], [null!]);

        Assert.Throws<ReservationDomain.InvalidStructureEditException>(() =>
            ReservationDomain.StructureEditPolicy.ValidateOutcome(invalidBackup));
        Assert.Throws<ReservationDomain.InvalidStructureEditException>(() =>
            ReservationDomain.StructureEditPolicy.ValidateOutcome(duplicates));
        Assert.Throws<ReservationDomain.InvalidStructureEditException>(() =>
            ReservationDomain.StructureEditPolicy.ValidateOutcome(nullEntry));
    }

    private sealed class WorkflowFixture :
        IOwnerAuthorizer, ISlotOperatorAuthorizer, IStructureCommitments, IParkingStructureRepository
    {
        public List<string> Events { get; } = [];
        public OwnerScope Owner { get; } = new(Guid.NewGuid(), Guid.NewGuid());
        public Guid SiteId { get; } = Guid.NewGuid();
        public Guid SlotId { get; }
        public Guid AcquiredToken { get; private set; }
        public ParkingStructure Structure { get; }
        public MemoryStore Store { get; }
        public StructureSynchronizationService Synchronization { get; }
        public ParkingStructureService Service { get; }
        public bool Authorized { get; set; } = true;
        public bool FailRelease { get; set; }
        public bool RevokeDuringAcquire { get; set; }
        public Guid? FailingSite { get; set; }

        public WorkflowFixture()
        {
            Structure = new(new(SiteId, Owner.TenantId, "TEST", "Test", "Address", null, null, true, 0));
            var unit = Structure.AddUnit(UnitType.ZONE, "A");
            SlotId = Structure.AddSlot(unit, "A01", VehicleType.CAR);
            Store = new MemoryStore(Events);
            Synchronization = new(Store, this);
            Service = new(this, this, this, this, Store, Synchronization);
        }

        public Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken ct)
        {
            Events.Add("authorize");
            return Task.FromResult(Authorized);
        }
        public Task<OwnerScope> ScopeAsync(Guid site, CancellationToken ct) => Task.FromResult(Owner);
        public Task<bool> IsAuthorizedAsync(OwnerScope actor, Guid site, CancellationToken ct)
            => IsOwnerAsync(actor, ct);

        public Task<CommitmentSnapshot> AcquireAsync(Guid tenant, Guid site, Guid token, CancellationToken ct)
        {
            Events.Add("acquire");
            AcquiredToken = token;
            if (RevokeDuringAcquire) Authorized = false;
            return Task.FromResult(new CommitmentSnapshot([], false));
        }
        public Task ReleaseAsync(Guid tenant, Guid site, Guid token, StructureOutcome? outcome, CancellationToken ct)
        {
            Events.Add(outcome is null ? "release-abort" : "release-outcome");
            if (FailRelease || site == FailingSite)
                throw new StructureException("COMMITMENTS_UNAVAILABLE", "Simulated lost response.");
            return Task.CompletedTask;
        }

        public Task<IParkingStructureEdit> BeginEditAsync(
            OwnerScope owner, Guid site, CommitmentSnapshot snapshot, Guid? operation, CancellationToken ct)
        {
            Events.Add("begin");
            return Task.FromResult<IParkingStructureEdit>(new MemoryEdit(this));
        }
        public Task<ParkingStructure> ReadAsync(OwnerScope owner, Guid site, CommitmentSnapshot snapshot, CancellationToken ct)
            => Task.FromResult(Structure);
        public Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SiteProfile>>([Structure.Site]);
        public Task<SiteProfile> CreateAsync(
            OwnerScope owner, SiteProfile site, CancellationToken ct)
            => throw new NotSupportedException();

        private sealed class MemoryEdit(WorkflowFixture fixture) : IParkingStructureEdit
        {
            public ParkingStructure Structure => fixture.Structure;
            public Task CommitAsync(Guid token, StructureOutcome outcome, Guid? marked, string? reason, CancellationToken ct)
            {
                fixture.Events.Add("commit");
                var index = fixture.Store.Rows.FindIndex(row => row.Token == token);
                fixture.Store.Rows[index] = fixture.Store.Rows[index] with { State = "ready", Outcome = outcome };
                return Task.CompletedTask;
            }
            public ValueTask DisposeAsync()
            {
                fixture.Events.Add("dispose-edit");
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class MemoryStore(List<string> events) : IStructureSynchronizationStore
    {
        public List<StructureSynchronization> Rows { get; } = [];
        public bool LockAvailable { get; set; } = true;
        public StructureOutcome? CommittedDuringFence { get; set; }

        public Task<IAsyncDisposable?> TryLockSiteAsync(Guid site, CancellationToken ct)
        {
            events.Add("lock");
            return Task.FromResult<IAsyncDisposable?>(LockAvailable ? new Gate(events) : null);
        }
        public Task PrepareAsync(StructureSynchronization row, CancellationToken ct)
        {
            events.Add("prepare");
            Rows.Add(row);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<StructureSynchronization>> PendingAsync(Guid? site, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<StructureSynchronization>>(
                Rows.Where(row => row.State != "completed" && (site is null || row.SiteId == site)).ToArray());
        public Task<StructureSynchronization> FencePreparationAsync(Guid token, CancellationToken ct)
        {
            var index = Rows.FindIndex(row => row.Token == token);
            Rows[index] = CommittedDuringFence is { } outcome
                ? Rows[index] with { State = "ready", Outcome = outcome }
                : Rows[index] with { State = "aborting" };
            return Task.FromResult(Rows[index]);
        }
        public Task AcknowledgeAsync(Guid token, CancellationToken ct)
        {
            events.Add("acknowledge");
            var index = Rows.FindIndex(row => row.Token == token);
            Rows[index] = Rows[index] with { State = "completed" };
            return Task.CompletedTask;
        }
        private sealed class Gate(List<string> events) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                events.Add("unlock");
                return ValueTask.CompletedTask;
            }
        }
    }
}
