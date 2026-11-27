namespace SmartParking.OperationService.Application.Discovery;

public sealed record FacilitySiteLocation(Guid SiteId, string SiteName, decimal Latitude, decimal Longitude);
