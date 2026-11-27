namespace SmartParking.OperationService.Domain.PassPlans;

public sealed record PassPlanDraftSpec(
    Guid? ZoneId,
    IReadOnlyList<PassVehicleType> VehicleTypes,
    OrdinaryCoverageRules OrdinaryCoverageRules,
    bool IsAvailableOnly);
