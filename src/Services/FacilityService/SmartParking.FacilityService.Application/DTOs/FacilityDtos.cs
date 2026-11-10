using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.DTOs;

public sealed record GateSlotClaim(Guid TenantId, string VehicleType, Guid? PreferredSlotId = null, Guid? TargetUnitId = null, bool AllowReservedSlot = false);
public sealed record GateSlotRelease(Guid TenantId);
public sealed record BackupMarkBody(string Reason);
public sealed record OperationBody(Guid IdempotencyKey, StructureEdit Edit, Guid? TenantId = null);
public sealed record CreateSite(string Code, string Name, string Address, decimal? Latitude, decimal? Longitude, Guid? TenantId = null);
public sealed record UnitBody(UnitType Type, string Name, Guid? ParentId, int Capacity, Guid? TenantId = null);
public sealed record SlotBody(Guid UnitId, string Code, VehicleType? VehicleType, SlotType Type = SlotType.STANDARD, IReadOnlyList<VehicleType>? VehicleTypes = null, VehicleType? PrimaryVehicleType = null, Guid? TenantId = null);
public sealed record PathBody(string Code, Guid? From, Guid? To, string? MapData, Guid? TenantId = null);
public sealed record EditStructure(string Action, Guid ResourceId, Guid UnitId, string? Code, string? Name, string? Address, bool Active, int Capacity, VehicleType? VehicleType, SlotType SlotType, IReadOnlyList<VehicleType>? VehicleTypes = null, VehicleType? PrimaryVehicleType = null, string? Coordinates3D = null, string? Features = null, decimal? Latitude = null, decimal? Longitude = null, Guid? TenantId = null);
public sealed record MaintenanceWindowBody(DateTime Start, DateTime End, string Reason, Guid? TenantId = null);
public sealed record MaintenanceWindowPatch(DateTime? Start, DateTime? End, Guid? TenantId = null);
public sealed record BatchBody(IReadOnlyList<BatchItemBody>? Items, Guid? IdempotencyKey = null, Guid? TenantId = null);
public sealed record BatchItemBody(Guid ResourceId, string Action, DateTime? ExpectedUpdatedAt = null);

public sealed record ParkingStructureResponse(
    SiteProfile Site,
    IReadOnlyList<SpatialUnit> Units,
    IReadOnlyList<ParkingSlot> Slots,
    IReadOnlyList<AccessPath> Paths,
    IReadOnlyList<BackupPolicy> BackupPolicies,
    IReadOnlyList<CapacityView> CapacityViews,
    ServiceStateResponse ServiceState);

public sealed record ServiceStateResponse(
    string State,
    DateTime? ClosedUntil,
    IEnumerable<MaintenanceWindowResponseItem> Windows);

public sealed record MaintenanceWindowResponseItem(
    Guid Id,
    DateTime Start,
    DateTime End,
    string Reason,
    string EnforcementState);

public sealed record OperatorStructureResponse(
    SiteProfile Site,
    IReadOnlyList<SpatialUnit> Units,
    IReadOnlyList<ParkingSlot> Slots,
    IReadOnlyList<AccessPath> Paths,
    IReadOnlyList<BackupPolicy> BackupPolicies,
    IReadOnlyList<CapacityView> CapacityViews);

