namespace SmartParking.OperationService.Application.Passes;

public sealed record PassPlanSnapshotRequest(
    Guid SiteId,
    Guid PlanId,
    int ExpectedPlanRevision,
    string ProductType,
    string VehicleType);
