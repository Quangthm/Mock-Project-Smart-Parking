using SmartParking.OperationService.Domain;

namespace SmartParking.UserService.Tests;

public sealed class DiscoveryCapacityTests
{
    private static readonly DateTimeOffset Now = new(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Site = Guid.NewGuid(), A = Guid.NewGuid(), B = Guid.NewGuid();
    private static CapacityEvidence Evidence() => new(Site, "CAR", "config", "policy", Now,
        Now.AddHours(-1), Now.AddDays(1), true, [new(A), new(B)], []);
    private static CapacityResult Run(CapacityEvidence? evidence, CancellationToken ct = default) =>
        DiscoveryCapacity.Evaluate(Site, "CAR", "config", Now, Now.AddHours(1), Now,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1), evidence, ct);

    [Fact]
    public void WholeIntervalRequiresTheSameFreeResource()
    {
        // A occupied first half, B occupied second half: no resource free for the whole interval.
        var evidence = Evidence() with { Blocks = [new(A, Now, Now.AddMinutes(30)), new(B, Now.AddMinutes(30), Now.AddHours(1))] };
        Assert.Equal(0, Run(evidence).Count);
    }

    [Fact]
    public void HalfOpenBoundariesAndCorrelatedClaimsAreUnioned()
    {
        var evidence = Evidence() with { Blocks = [new(A, Now, Now.AddMinutes(20)), new(A, Now, Now.AddHours(1)),
            new(B, Now.AddHours(-1), Now), new(B, Now.AddHours(1), Now.AddHours(2))] };
        Assert.Equal(1, Run(evidence).Count);
        // Departure removing one overlapping block does not release the other Driver's protection.
        Assert.Equal(1, Run(evidence with { Blocks = evidence.Blocks.Skip(1).ToArray() }).Count);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    [InlineData(-1, true)]
    [InlineData(-2, false)]
    public void AgeAndSkewBoundariesRetainEvidenceTimestamp(int ageSeconds, bool known)
    {
        var evidence = Evidence() with { AsOf = Now.AddSeconds(-ageSeconds) };
        var result = Run(evidence);
        Assert.Equal(known ? 2 : null, result.Count);
        Assert.Equal(evidence.AsOf, result.AsOf);
        Assert.Equal(known ? null : "stale", result.Reason);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("partial")]
    [InlineData("configuration")]
    [InlineData("horizon")]
    public void MissingCoverageCannotProveFreeCapacity(string mode)
    {
        var evidence = mode switch
        {
            "missing" => null,
            "partial" => Evidence() with { Complete = false },
            "configuration" => Evidence() with { ConfigurationVersion = "different" },
            _ => Evidence() with { To = Now.AddMinutes(59) }
        };
        Assert.Equal(new CapacityResult(null, null, "unknown"), Run(evidence));
    }

    [Theory]
    [InlineData("site")]
    [InlineData("category")]
    [InlineData("duplicate")]
    [InlineData("foreign-block")]
    [InlineData("backwards-block")]
    [InlineData("bounds")]
    public void ContradictoryOrUnboundedEvidenceFails(string mode)
    {
        var evidence = mode switch
        {
            "site" => Evidence() with { SiteId = Guid.NewGuid() },
            "category" => Evidence() with { VehicleType = "MOTORCYCLE" },
            "duplicate" => Evidence() with { Resources = [new(A), new(A)] },
            "foreign-block" => Evidence() with { Blocks = [new(Guid.NewGuid(), Now, Now.AddHours(1))] },
            "backwards-block" => Evidence() with { Blocks = [new(A, Now.AddHours(1), Now)] },
            _ => Evidence() with { Resources = Enumerable.Range(0, 10001).Select(_ => new CapacityResource(Guid.NewGuid())).ToArray() }
        };
        Assert.Throws<InvalidCapacityEvidenceException>(() => Run(evidence));
    }

    [Fact]
    public void CancellationAndUnsupportedSourceDoNotReturnKnownCount()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Run(Evidence(), cts.Token));
        Assert.Null(Run(null).Count);
        Assert.Throws<InvalidCapacityEvidenceException>(() => DiscoveryCapacity.Evaluate(Site, "CAR", "config",
            Now, Now, Now, TimeSpan.FromSeconds(60), TimeSpan.Zero, Evidence(), default));
    }
}
