using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Tests;

public sealed class OperationalLayoutReadPathTests
{
    [Fact]
    public async Task ReadOperationalAsyncDoesNotCallStructureCommitments()
    {
        var commitments = new RecordingCommitments();
        var readResult = new OperationalLayoutReadResult(
            BuildProjection(),
            14,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["Zone_A"] = 10 });
        var service = new ParkingStructureService(
            repository: null!,
            owners: null!,
            operators: null!,
            commitments: commitments,
            synchronizationStore: null!,
            synchronization: null!,
            operationalLayouts: new StubReader(readResult),
            callerResolver: new StubResolver());

        var layout = await service.ReadOperationalAsync(
            Guid.NewGuid(), vehicleType: null, floor: null, CancellationToken.None);

        Assert.Equal(0, commitments.ReadSnapshotCalls);
        Assert.Equal(0, commitments.AcquireCalls);
        Assert.Equal(0, commitments.ReleaseCalls);

        // The reader's full-site masked counts flow through the single redaction choke point.
        Assert.Equal(14, layout.Metrics.ReservedCount);
        Assert.Equal(10, layout.Metrics.ZoneCapacities[0].ZoneOnlyReservedCount);
        Assert.Equal(ReservationState.RESERVED, layout.Slots[0].ReservationState);
    }

    private sealed class RecordingCommitments : IStructureCommitments
    {
        public int ReadSnapshotCalls { get; private set; }
        public int AcquireCalls { get; private set; }
        public int ReleaseCalls { get; private set; }
        public int ForceReleaseCalls { get; private set; }

        public Task<CommitmentSnapshot> ReadSnapshotAsync(
            Guid tenantId, Guid siteId, CancellationToken cancellationToken)
        {
            ReadSnapshotCalls++;
            throw new InvalidOperationException(
                "ReadOperationalAsync must not read a structure commitment snapshot.");
        }

        public Task<CommitmentSnapshot> AcquireAsync(
            Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken)
        {
            AcquireCalls++;
            throw new InvalidOperationException(
                "ReadOperationalAsync must not acquire a structure commitment hold.");
        }

        public Task ReleaseAsync(
            Guid tenantId, Guid siteId, Guid token, StructureOutcome? outcome,
            CancellationToken cancellationToken)
        {
            ReleaseCalls++;
            throw new InvalidOperationException(
                "ReadOperationalAsync must not release a structure commitment hold.");
        }

        public Task ForceReleaseAsync(
            Guid tenantId, Guid siteId, CancellationToken cancellationToken)
        {
            ForceReleaseCalls++;
            throw new InvalidOperationException(
                "ReadOperationalAsync must not force-release a structure commitment hold.");
        }
    }

    private sealed class StubReader(OperationalLayoutReadResult result) : IOperationalLayoutReader
    {
        public Task<OperationalLayoutReadResult> ReadAsync(
            LayoutReadScope scope, Guid siteId, VehicleType? vehicleType, string? floor,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class StubResolver : ILayoutCallerResolver
    {
        public Task<LayoutCaller> ResolveAsync(Guid siteId, CancellationToken cancellationToken)
            => Task.FromResult(new LayoutCaller(
                LayoutCallerKind.Driver, Guid.Empty, Array.Empty<Guid>(), IsSiteTelemetryGranted: false));
    }

    private static OperationalLayoutProjectionDto BuildProjection()
    {
        var now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        var slot = new SlotVisualStateDto(
            Guid.Empty, "CarParkingSpace_001", Guid.Empty, "Floor_G", "Zone_A", "Row_01",
            VehicleType.CAR, SlotType.STANDARD, PhysicalState.AVAILABLE, ReservationState.BACKUP,
            SessionType.STANDARD, false, false, null, null, null, now);

        return new OperationalLayoutProjectionDto(
            Guid.Empty,
            "SITE_TEST_01",
            "Contract Test Site",
            OperationalLayoutContract.Version,
            now,
            true,
            OperationalLayoutContract.StalenessThresholdSeconds,
            new LayoutMetricsDto(
                1,
                0,
                0,
                0,
                new MotorcycleCapacityDto(0, 0, 0),
                new[] { new ZoneCapacityDto(Guid.Empty, "Zone_A", "Floor_G", "North", 1, 0, 1, 1) }),
            new[] { new FloorProjectionDto(Guid.Empty, "Floor_G", "Ground", 1, 1) },
            new[] { slot });
    }
}
