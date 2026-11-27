namespace SmartParking.OperationService.Domain.PassPlans;

public sealed record OrdinaryCoverageRules(
    bool DayCovered,
    bool NightCovered,
    IReadOnlyList<Guid> ExcludedZoneIds);
