namespace SmartParking.FacilityService.Application.Reservations;

public sealed record SelectedSlotMetadata(Guid SlotId, Guid SiteId, Guid TenantId);
