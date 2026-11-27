namespace SmartParking.OperationService.Application.Discovery;

public sealed record SearchFilter(double Lat, double Lon, string VehicleType, double RadiusKm, string? LotType,
    bool AvailableOnly, DateTimeOffset? StartAt, DateTimeOffset? EndAt, string TimeZone, long? MinTotalVnd,
    long? MaxTotalVnd, int Limit, string? Keyword = null, string Sort = "distance");
