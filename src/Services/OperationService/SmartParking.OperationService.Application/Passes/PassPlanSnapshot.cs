using System.Text.Json;

namespace SmartParking.OperationService.Application.Passes;

public sealed record PassPlanSnapshot(
    Guid PlanId,
    int PlanRevision,
    string ProductType,
    string EntitlementType,
    Guid SiteId,
    Guid? ZoneId,
    string VehicleType,
    string TimeZoneId,
    JsonElement OrdinaryCoverageRules,
    JsonElement EffectivePolicyParams);
