namespace SmartParking.OperationService.Infrastructure.Messaging;

public static class FacilityLayoutInvalidationTopology
{
    public const int CurrentSchemaVersion = 1;
    public const string ExchangeName = "smartpark.facility.layout-invalidated.v1";
    public const string QueueName = "smartpark-operation-layout-invalidated-v1";
    public const string FullLayout = "full-layout";
    public const string CapacitySummary = "capacity-summary";
    public const string Structure = "structure";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        FullLayout,
        CapacitySummary,
        Structure
    };

    public static string[] NormalizeKnownResources(IEnumerable<string>? resources)
    {
        if (resources is null) return [];
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in resources)
        {
            if (string.IsNullOrWhiteSpace(resource)) continue;
            var candidate = resource.Trim();
            if (Allowed.Contains(candidate) && seen.Add(candidate))
                normalized.Add(candidate);
        }
        return [.. normalized];
    }
}
