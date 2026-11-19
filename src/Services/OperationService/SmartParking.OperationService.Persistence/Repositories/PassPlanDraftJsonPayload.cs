namespace SmartParking.OperationService.Persistence.Repositories;

internal sealed record PassPlanDraftJsonPayload(
    Guid? ZoneId,
    IReadOnlyList<string> VehicleTypes,
    PassPlanCoverageJsonPayload OrdinaryCoverageRules,
    bool IsAvailableOnly);
