namespace SmartParking.OperationService.Domain;

// Resolved owner evidence only. These are not transport DTOs or an authorization grant.
// One resource represents one category-compatible vehicle for the entire requested interval.
public sealed record CapacityResource(Guid Id);
public sealed record CapacityBlock(Guid ResourceId, DateTimeOffset From, DateTimeOffset To);
// Complete attests a coherent, nonoverlapping fixed-resource universe for the entire site/category,
// all claims/physical occupancy/service restrictions, effective policy and participating writer watermarks.
// Capacity-only/shared pools, unlocated occupants and unverified departure are unsupported until resolved
// by a real owner producer. This pure evaluator cannot establish provenance or completeness itself.
public sealed record CapacityEvidence(Guid SiteId, string VehicleType, string ConfigurationVersion,
    string PolicyVersion, DateTimeOffset AsOf, DateTimeOffset From, DateTimeOffset To,
    bool Complete, CapacityResource[] Resources, CapacityBlock[] Blocks);
public sealed record CapacityResult(int? Count, DateTimeOffset? AsOf, string? Reason);
public sealed class InvalidCapacityEvidenceException : Exception;

public static class DiscoveryCapacity
{
    public static CapacityResult Evaluate(Guid siteId, string vehicleType, string configurationVersion,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, TimeSpan maxAge, TimeSpan skew,
        CapacityEvidence? evidence, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (from >= to || maxAge <= TimeSpan.Zero || skew < TimeSpan.Zero)
            throw new InvalidCapacityEvidenceException();
        if (evidence is null) return new(null, null, "unknown");
        if (evidence.SiteId != siteId || evidence.VehicleType != vehicleType ||
            string.IsNullOrWhiteSpace(evidence.ConfigurationVersion) || string.IsNullOrWhiteSpace(evidence.PolicyVersion) ||
            evidence.AsOf == default || evidence.From == default || evidence.To <= evidence.From ||
            evidence.Resources is null || evidence.Blocks is null ||
            evidence.Resources.Length > 10000 || evidence.Blocks.Length > 10000)
            throw new InvalidCapacityEvidenceException();
        var resources = new HashSet<Guid>();
        foreach (var resource in evidence.Resources)
        {
            ct.ThrowIfCancellationRequested();
            if (resource is null || resource.Id == Guid.Empty || !resources.Add(resource.Id))
                throw new InvalidCapacityEvidenceException();
        }
        var blocked = new HashSet<Guid>();
        foreach (var block in evidence.Blocks)
        {
            ct.ThrowIfCancellationRequested();
            if (block is null || !resources.Contains(block.ResourceId) || block.From == default || block.To <= block.From)
                throw new InvalidCapacityEvidenceException();
            if (block.From < to && from < block.To) blocked.Add(block.ResourceId);
        }
        if (now - evidence.AsOf > maxAge || evidence.AsOf - now > skew)
            return new(null, evidence.AsOf, "stale");
        if (!evidence.Complete || evidence.ConfigurationVersion != configurationVersion ||
            evidence.From > from || evidence.To < to)
            return new(null, null, "unknown");
        return new(resources.Count - blocked.Count, evidence.AsOf, null);
    }
}
