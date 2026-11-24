namespace SmartParking.OperationService.API.Controllers;

public sealed record ApplyMaintenanceWindowRequest(
    Guid TenantId,
    Guid? UnitId,
    Guid? SlotId,
    IReadOnlyList<Guid>? ScopedUnitIds,
    IReadOnlyList<Guid>? ScopedSlotIds,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Reason);