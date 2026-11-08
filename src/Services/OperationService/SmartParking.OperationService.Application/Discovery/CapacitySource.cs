using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Discovery;

public interface IDiscoveryCapacitySource
{
    // Null means the source is explicitly unsupported, not an outage or an empty lot.
    // Expected dependency outages must throw DiscoveryFailure.Unavailable(), never return null.
    // Complete evidence covers the entire fixed-resource universe, all restrictions, occupancy,
    // correlated independent claims, effective policy and coherent participating-writer watermarks.
    // Physical passage must be verified by an approved producer, never inferred from checkout/time.
    // The adapter must resolve and verify trusted site-to-tenant attribution before building evidence;
    // the pure evaluator checks site/category only and is not a tenant authorization mechanism.
    Task<CapacityEvidence?> Read(DiscoverySite site, SearchFilter filter, DateTimeOffset at, CancellationToken ct);
}

// No trusted physical/claim coverage producer exists in the current checkout.
// This registration intentionally cannot supply a positive count. Never replace it with counters/fixtures.
public sealed class UnsupportedDiscoveryCapacity : IDiscoveryCapacitySource
{
    public Task<CapacityEvidence?> Read(DiscoverySite site, SearchFilter filter, DateTimeOffset at, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<CapacityEvidence?>(null);
    }
}
