namespace SmartParking.FacilityService.Application.Discovery;

public sealed record PublishedSiteLocation(Guid SiteId, string SiteName, decimal Latitude, decimal Longitude);
