namespace SmartParking.OperationService.Application.Reservations;

public sealed record SelectedSlotMetadata(Guid SlotId, Guid SiteId, Guid TenantId);
