namespace SmartParking.OperationService.Application.Discovery;

public sealed record DiscoverySite(Guid SiteId, string Name, string Address, double Latitude, double Longitude,
    string LotType, string[] SupportedVehicleTypes, string ConfigurationVersion, DateTimeOffset AsOf, FacilityWindow? ServiceWindow = null);
