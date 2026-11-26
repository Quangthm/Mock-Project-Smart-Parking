namespace SmartParking.OperationService.Persistence.Repositories;

internal sealed record PassPlanCoverageJsonPayload(
    bool DayCovered,
    bool NightCovered,
    IReadOnlyList<Guid> ExcludedZoneIds);
