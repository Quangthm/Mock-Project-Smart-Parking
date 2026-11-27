namespace SmartParking.OperationService.Application.Discovery;

public sealed record ParkingSummary(Guid SiteId, string Name, string Address, double Latitude, double Longitude,
    string LotType, string[] SupportedVehicleTypes, double DistanceMeters, Availability Availability,
    DiscoveryPrice? Price, SourceTimes SourceAsOf, FieldStates FieldStatus);
